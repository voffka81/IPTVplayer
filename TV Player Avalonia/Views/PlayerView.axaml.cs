using Avalonia.Controls;
using Avalonia;
using Avalonia.Input;
using Avalonia.VisualTree;
using Avalonia.Threading;
using Avalonia.Controls.Primitives;
using LibVLCSharp.Shared;
using NAudio.CoreAudioApi;
using System.Diagnostics;
using System.ComponentModel;
using System.Threading.Tasks;
using System.Runtime.InteropServices;
using TV_Player.AvaloniaApp.Services;
using TV_Player.AvaloniaApp.ViewModels;
using TV_Player.Input;
using TV_Player.Playback;

namespace TV_Player.AvaloniaApp.Views;

public partial class PlayerView : UserControl
{
    private readonly DispatcherTimer _clockTimer = new() { Interval = TimeSpan.FromSeconds(10) };
    private readonly DispatcherTimer _overlayTimer = new() { Interval = TimeSpan.FromSeconds(5) };
    private bool _overlayVisible = true;
    private bool _hasPlaybackStarted => _viewModel?.Playback.Snapshot.HasStarted == true;
    private bool CanAutoHideControls => _viewModel?.Playback.Snapshot.CanAutoHideControls == true;
    private PlaybackHostLease? _playbackLease;
    private AvaloniaPlaybackController? _playback;
    private PlayerViewModel? _viewModel;
    private bool _initialized;
    private bool _initializing;
    private int _attachmentGeneration;
    private MMDeviceEnumerator? _audioDeviceEnumerator;
    private MMDevice? _masterAudioDevice;
    private int _systemVolumeBeforeMute = 100;
    private bool _updatingMasterVolume;
    private readonly DispatcherTimer _gamepadTimer = new() { Interval = TimeSpan.FromMilliseconds(100) };
    private ushort _previousGamepadButtons;
    private IntPtr _keyboardHook;
    private LowLevelKeyboardProc? _keyboardHookProc;

    private delegate IntPtr LowLevelKeyboardProc(int code, IntPtr wParam, IntPtr lParam);

    [StructLayout(LayoutKind.Sequential)]
    private struct LowLevelKeyboardData
    {
        public uint VirtualKey;
        public uint ScanCode;
        public uint Flags;
        public uint Time;
        public IntPtr ExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct XInputGamepad
    {
        public ushort Buttons;
        public byte LeftTrigger;
        public byte RightTrigger;
        public short ThumbLX;
        public short ThumbLY;
        public short ThumbRX;
        public short ThumbRY;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct XInputState
    {
        public uint PacketNumber;
        public XInputGamepad Gamepad;
    }

    [DllImport("xinput1_4.dll", EntryPoint = "XInputGetState")]
    private static extern uint XInputGetState(uint userIndex, out XInputState state);

    private const int WhKeyboardLowLevel = 13;
    private const int WmKeyDown = 0x0100;
    private const int WmSysKeyDown = 0x0104;
    private const uint VkLeft = 0x25;
    private const uint VkRight = 0x27;

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr SetWindowsHookEx(int hookType, LowLevelKeyboardProc callback, IntPtr module, uint threadId);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool UnhookWindowsHookEx(IntPtr hook);

    [DllImport("user32.dll")]
    private static extern IntPtr CallNextHookEx(IntPtr hook, int code, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint GetWindowThreadProcessId(IntPtr window, out uint processId);

    public PlayerView()
    {
        InitializeComponent();
        MasterVolumePanel.IsVisible = OperatingSystem.IsWindows();
        PlayerClock.Text = DateTime.Now.ToString("HH:mm");
        _clockTimer.Tick += (_, _) => PlayerClock.Text = DateTime.Now.ToString("HH:mm");
        SizeChanged += OnPlayerSizeChanged;
        UpdateResponsiveControls(Bounds.Size);
        _overlayTimer.Tick += OnOverlayTimerTick;
        _gamepadTimer.Tick += OnGamepadTimerTick;
        Log("PlayerView ctor");
        DataContextChanged += OnDataContextChanged;
        AttachedToVisualTree += OnAttachedToVisualTree;
        DetachedFromVisualTree += OnDetachedFromVisualTree;
    }

    private void OnPlayerSizeChanged(object? sender, SizeChangedEventArgs e)
    {
        UpdateResponsiveControls(e.NewSize);
    }

    private void UpdateResponsiveControls(Size size)
    {
        // Avalonia layout units already account for OS DPI scaling.
        // Scale the overlay with the available video viewport rather than fixed 48px buttons.
        if (size.Width <= 0 || size.Height <= 0)
            return;

        var scale = Math.Clamp(Math.Min(size.Width / 800.0, size.Height / 450.0), 0.85, 1.8);
        var barHeight = Math.Clamp(80 * scale, 68, 140);
        OverlayPanel.RowDefinitions[0].Height = new GridLength(barHeight);
        PlayerBottomBar.Height = barHeight;

        var buttonSize = Math.Clamp(54 * scale, 48, 94);
        foreach (var button in new[] { PlayerNav1, PlayerNav2, PlayerNav3, PlayerNav4 })
        {
            button.Width = buttonSize;
            button.Height = buttonSize;
            button.Margin = new Thickness(0);
            button.HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center;
            button.VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center;
        }

        var topColumnWidth = Math.Max(buttonSize + 16, 68);
        foreach (var column in PlayerTopBar.ColumnDefinitions)
        {
            if (column.Width.IsAbsolute)
                column.Width = new GridLength(topColumnWidth);
        }

        var leftColumnWidth = Math.Max(buttonSize + 32 * scale, 112 * scale);
        var masterVolumeSliderWidth = Math.Clamp(size.Width * 0.07, 40, 70);
        var rightColumnWidth = Math.Max(160, masterVolumeSliderWidth + 84);
        PlayerBottomBar.ColumnDefinitions[0].Width = new GridLength(leftColumnWidth);
        PlayerBottomBar.ColumnDefinitions[2].Width = new GridLength(rightColumnWidth);
        var centerColumnWidth = Math.Max(0, size.Width - leftColumnWidth - rightColumnWidth);
        ProgramBar.SetAvailableWidth(centerColumnWidth);
        MasterVolumeSlider.Width = masterVolumeSliderWidth;
    }

    private void OnAttachedToVisualTree(object? sender, Avalonia.VisualTreeAttachmentEventArgs e)
    {
        if (_initialized || _initializing)
            return;
        if (!TryReleasePlayback(_playbackLease)) return;

        if (_viewModel != null)
        {
            _viewModel.PropertyChanged -= OnViewModelPropertyChanged;
            _viewModel.PropertyChanged += OnViewModelPropertyChanged;
        }
        _clockTimer.Start();
        if (OperatingSystem.IsWindows())
        {
            _gamepadTimer.Start();
            InstallKeyboardHook();
        }
        ShowOverlay();
        InitializeMasterVolume();
        Log("Attach to visual tree -> initializing VLC");
        _initializing = true;
        _ = InitializeVlcAsync(++_attachmentGeneration);
    }

    private async Task InitializeVlcAsync(int generation)
    {
        LibVLC? libVlc = null;
        MediaPlayer? mediaPlayer = null;
        PlaybackHostLease? lease = null;
        try
        {
            libVlc = await LibVlcRuntime.InitializeAsync();
            mediaPlayer = await Task.Run(() =>
            {
                return new MediaPlayer(libVlc)
                {
                    // Software decode is more reliable for mixed IPTV codecs on macOS.
                    EnableHardwareDecoding = !OperatingSystem.IsMacOS(),
                    Volume = 100,
                    Mute = false
                };
            });

            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                if (generation != _attachmentGeneration)
                {
                    mediaPlayer?.Dispose();
                    return;
                }

                _playback = new AvaloniaPlaybackController(libVlc, mediaPlayer, _viewModel, ShowOverlay, OnPlayingReconciled, Log);
                lease = _playbackLease = new PlaybackHostLease(DetachVideoHost, _playback, mediaPlayer);

                var videoView = this.FindControl<LibVLCSharp.Avalonia.VideoView>("VideoView")
                    ?? throw new InvalidOperationException("VideoView control was not found in PlayerView.");
                videoView.MediaPlayer = mediaPlayer;
                _initialized = true;
                _initializing = false;
                Log("LibVLC and MediaPlayer initialized; VideoView assigned");
                _playback.PlayCurrentStream();
            });
        }
        catch (System.Exception ex)
        {
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                // Cleanup belongs on the UI thread, with host detachment first.
                if (lease != null) TryReleasePlayback(lease); else mediaPlayer?.Dispose();
                if (generation != _attachmentGeneration)
                    return;
                _initializing = false;
                Log($"VLC initialization failed: {ex}");
                if (_viewModel != null)
                {
                    _viewModel.Playback.Apply(new(PlayerPlaybackCommandKind.Fail, _viewModel.Playback.Snapshot.Token.Generation,
                        Failure: PlayerPlaybackFailure.EngineUnavailable, Cause: ex));
                    _viewModel.SetPlaybackStatus($"VLC init failed: {ex.Message}");
                }
            });
        }
    }

    private void OnDetachedFromVisualTree(object? sender, Avalonia.VisualTreeAttachmentEventArgs e)
    {
        _attachmentGeneration++;
        _playback?.Suspend();
        _initializing = false;
        _clockTimer.Stop();
        _overlayTimer.Stop();
        _gamepadTimer.Stop();
        RemoveKeyboardHook();
        Log("Detach from visual tree -> disposing player resources");
        if (_viewModel != null)
        {
            _viewModel.PropertyChanged -= OnViewModelPropertyChanged;
        }

        try { TryReleasePlayback(_playbackLease); }
        finally
        {
            _initialized = false;
            DisposeMasterVolume();
        }
    }

    private bool TryReleasePlayback(PlaybackHostLease? lease)
    {
        try { lease?.Dispose(); }
        catch (Exception error) { Log($"Playback cleanup failed: {error}"); }
        // Retain an attached lease for retry instead of losing the live native pointer.
        if (lease?.IsReleased == false) return false;
        if (ReferenceEquals(lease, _playbackLease))
        {
            _playbackLease = null;
            _playback = null;
            _initialized = false;
        }
        return true;
    }

    private void DetachVideoHost()
    {
        // VideoView must stop referencing its player before that native pointer is freed.
        var videoView = this.FindControl<LibVLCSharp.Avalonia.VideoView>("VideoView");
        if (videoView != null) videoView.MediaPlayer = null;
        Log("VideoView.MediaPlayer detached before disposal");
    }

    private void OnGamepadTimerTick(object? sender, EventArgs e)
    {
        XInputState state = default;
        try
        {
            if (XInputGetState(0, out state) != 0)
            {
                _previousGamepadButtons = 0;
                return;
            }
        }
        catch (DllNotFoundException)
        {
            _previousGamepadButtons = 0;
            _gamepadTimer.Stop();
            return;
        }

        const ushort dpadLeft = 0x0004;
        const ushort dpadRight = 0x0008;
        var pressed = (ushort)(state.Gamepad.Buttons & (dpadLeft | dpadRight));
        var newlyPressed = (ushort)(pressed & ~_previousGamepadButtons);
        _previousGamepadButtons = pressed;

        if (_viewModel == null || newlyPressed == 0)
            return;

        DispatchHorizontalInput((newlyPressed & dpadRight) != 0 ? 1 : -1);
    }

    private void InstallKeyboardHook()
    {
        _keyboardHookProc = OnLowLevelKeyboard;
        _keyboardHook = SetWindowsHookEx(WhKeyboardLowLevel, _keyboardHookProc, IntPtr.Zero, 0);
        if (_keyboardHook == IntPtr.Zero)
            Log($"Could not install low-level keyboard hook: {Marshal.GetLastWin32Error()}");
    }

    private void RemoveKeyboardHook()
    {
        if (_keyboardHook != IntPtr.Zero)
            UnhookWindowsHookEx(_keyboardHook);
        _keyboardHook = IntPtr.Zero;
        _keyboardHookProc = null;
    }

    private IntPtr OnLowLevelKeyboard(int code, IntPtr wParam, IntPtr lParam)
    {
        if (code >= 0 && _viewModel != null &&
            (wParam.ToInt32() == WmKeyDown || wParam.ToInt32() == WmSysKeyDown))
        {
            var foreground = GetForegroundWindow();
            GetWindowThreadProcessId(foreground, out var foregroundProcessId);
            if (foregroundProcessId == (uint)Environment.ProcessId)
            {
                var key = Marshal.PtrToStructure<LowLevelKeyboardData>(lParam).VirtualKey;
                if (key == VkLeft || key == VkRight)
                {
                    Dispatcher.UIThread.Post(() =>
                    {
                        if (_viewModel != null)
                        {
                            DispatchHorizontalInput(key == VkRight ? 1 : -1);
                        }
                    });
                    return new IntPtr(1);
                }
            }
        }

        return CallNextHookEx(_keyboardHook, code, wParam, lParam);
    }

    private void InitializeMasterVolume()
    {
        if (!OperatingSystem.IsWindows())
            return;

        try
        {
            _audioDeviceEnumerator = new MMDeviceEnumerator();
            _masterAudioDevice = _audioDeviceEnumerator.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia);
            var endpointVolume = _masterAudioDevice.AudioEndpointVolume;
            var volume = Math.Clamp((int)Math.Round(endpointVolume.MasterVolumeLevelScalar * 100), 0, 100);
            _systemVolumeBeforeMute = volume > 0 ? volume : 100;

            _updatingMasterVolume = true;
            MasterVolumeSlider.Value = volume;
            MasterVolumeSlider.IsEnabled = true;
            MasterVolumeMuteButton.IsEnabled = true;
            UpdateMasterVolumeControls(volume, endpointVolume.Mute);
            _updatingMasterVolume = false;
            Log($"Windows master volume initialized: {volume}%, muted={endpointVolume.Mute}");
        }
        catch (Exception ex)
        {
            _updatingMasterVolume = false;
            DisableMasterVolume(ex);
        }
    }

    private void OnMasterVolumeSliderValueChanged(object? sender, RangeBaseValueChangedEventArgs e)
    {
        if (_updatingMasterVolume || _masterAudioDevice == null)
            return;

        var volume = Math.Clamp((int)Math.Round(e.NewValue), 0, 100);
        try
        {
            var endpointVolume = _masterAudioDevice.AudioEndpointVolume;
            endpointVolume.MasterVolumeLevelScalar = volume / 100f;
            if (volume > 0)
            {
                _systemVolumeBeforeMute = volume;
                endpointVolume.Mute = false;
            }

            UpdateMasterVolumeControls(volume, endpointVolume.Mute);
        }
        catch (Exception ex)
        {
            DisableMasterVolume(ex);
        }
    }

    private void OnMasterVolumeMuteButtonClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (_masterAudioDevice == null)
            return;

        try
        {
            var endpointVolume = _masterAudioDevice.AudioEndpointVolume;
            var volume = Math.Clamp((int)Math.Round(endpointVolume.MasterVolumeLevelScalar * 100), 0, 100);
            if (endpointVolume.Mute || volume == 0)
            {
                endpointVolume.Mute = false;
                volume = Math.Clamp(_systemVolumeBeforeMute, 1, 100);
                MasterVolumeSlider.Value = volume;
                endpointVolume.MasterVolumeLevelScalar = volume / 100f;
            }
            else
            {
                _systemVolumeBeforeMute = volume;
                endpointVolume.Mute = true;
            }

            UpdateMasterVolumeControls(volume, endpointVolume.Mute);
        }
        catch (Exception ex)
        {
            DisableMasterVolume(ex);
        }
    }

    private void UpdateMasterVolumeControls(int volume, bool isMuted)
    {
        MasterVolumeLabel.Text = $"{volume}%";
        MasterVolumeMuteButton.Content = isMuted || volume == 0 ? "🔇" : "🔊";
    }

    private void DisableMasterVolume(Exception exception)
    {
        Log($"Windows master volume unavailable: {exception}");
        _viewModel?.SetPlaybackStatus($"Windows master volume unavailable: {exception.Message}");
        MasterVolumeSlider.IsEnabled = false;
        MasterVolumeMuteButton.IsEnabled = false;
        MasterVolumeLabel.Text = "N/A";
        DisposeMasterVolume();
    }

    private void DisposeMasterVolume()
    {
        _masterAudioDevice?.Dispose();
        _audioDeviceEnumerator?.Dispose();
        _masterAudioDevice = null;
        _audioDeviceEnumerator = null;
    }

    private void OnOverlayTimerTick(object? sender, System.EventArgs e)
    {
        _playback?.ReconcilePlayingUi();
        if (_viewModel?.ChannelPickerVisible == true)
        {
            _overlayTimer.Stop();
            _overlayTimer.Start();
            return;
        }

        if (CanAutoHideControls)
            HideOverlay();
    }

    private void ShowOverlay()
    {
        _overlayVisible = true;
        OverlayPanel.IsVisible = true;
        _overlayTimer.Stop();
        if (CanAutoHideControls)
            _overlayTimer.Start();
    }

    private void HideOverlay()
    {
        if (!CanAutoHideControls)
        {
            ShowOverlay();
            return;
        }

        _overlayTimer.Stop();
        _overlayVisible = false;
        OverlayPanel.IsVisible = false;
        if (_viewModel != null)
            _viewModel.ProgramGuideVisible = false;
    }

    private void OnOverlayPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (!_hasPlaybackStarted)
        {
            ShowOverlay();
            return;
        }

        // Buttons and other interactive elements retain their own commands.
        // A click on the video/background toggles the control overlay.
        if (e.Source is Avalonia.Visual source)
        {
            for (Avalonia.Visual? current = source; current != null && current != OverlayPanel; current = current.GetVisualParent())
            {
                if (current == PlayerBottomBar || current is Button || current is ScrollViewer)
                {
                    ShowOverlay();
                    return;
                }
            }
        }

        if (_overlayVisible)
            HideOverlay();
        else
            ShowOverlay();
    }

    private void DispatchHorizontalInput(int direction)
    {
        if (_viewModel == null) return;

        var context = PlayerInputRouter.GetContext(
            _viewModel.ProgramGuideVisible, _viewModel.ChannelPickerVisible);
        var action = PlayerInputRouter.RouteHorizontal(direction, context);
        switch (action.Action)
        {
            case PlayerInputAction.SwitchChannel:
                _viewModel.SwitchChannel(action.Direction);
                break;
            case PlayerInputAction.MoveProgramGuide:
                ScrollProgramGuide(action.Direction);
                break;
            case PlayerInputAction.MoveChannelPicker:
                MoveChannelPicker(action.Direction);
                break;
        }

        if (action.Action != PlayerInputAction.None)
            ShowOverlay();
    }

    private void MoveChannelPicker(int direction)
    {
        if (_viewModel == null || _viewModel.ChannelPicker.ChannelPickerItems.Count == 0)
            return;
        var items = _viewModel.ChannelPicker.ChannelPickerItems;
        var current = _viewModel.ChannelPicker.SelectedChannelPickerItem;
        var index = current == null ? 0 : items.IndexOf(current);
        var next = Math.Clamp(index + direction, 0, items.Count - 1);
        _viewModel.ChannelPicker.SelectedChannelPickerItem = items[next];
        ChannelPickerControl.ScrollIntoView(items[next]);
    }

    private void ScrollProgramGuide(int direction) => ProgramBar.ScrollGuide(direction);

    private void OnDataContextChanged(object? sender, System.EventArgs e)
    {
        Log($"DataContext changed. New type: {DataContext?.GetType().Name ?? "null"}");
        if (_viewModel != null)
        {
            _viewModel.PropertyChanged -= OnViewModelPropertyChanged;
        }

        _viewModel = DataContext as PlayerViewModel;
        if (_viewModel != null)
        {
            _viewModel.PropertyChanged += OnViewModelPropertyChanged;
        }

        _playback?.SetViewModel(_viewModel);
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(PlayerViewModel.StreamUrl))
        {
            Log("ViewModel StreamUrl changed -> replaying stream");
            _playback?.PlayCurrentStream();
        }
    }

    private void OnPlayingReconciled()
    {
        if (!_overlayVisible) return;
        _overlayTimer.Stop();
        _overlayTimer.Start();
    }

    private static void Log(string message)
    {
        var line = $"[PlayerView] {DateTime.Now:HH:mm:ss.fff} {message}";
        Debug.WriteLine(line);
        Console.WriteLine(line);

        try
        {
            var logPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "TVPlayer", "Logs", "player.log");
            Directory.CreateDirectory(Path.GetDirectoryName(logPath)!);
            File.AppendAllText(logPath, $"{DateTime.Now:yyyy-MM-dd} {line}{Environment.NewLine}");
        }
        catch
        {
            // Keep player diagnostics from interrupting playback when the log is unavailable.
        }
    }
}
