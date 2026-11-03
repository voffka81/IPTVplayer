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
using System.Threading;
using System.Threading.Tasks;
using System.Web;
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
    private readonly DispatcherTimer _playbackUiTimer = new() { Interval = TimeSpan.FromMilliseconds(250) };
    private bool _overlayVisible = true;
    private Action? _unsubscribePlayback;
    private bool _hasPlaybackStarted => _viewModel?.Playback.Snapshot.HasStarted == true;
    private bool CanAutoHideControls => _viewModel?.Playback.Snapshot.CanAutoHideControls == true;
    private LibVLC? _libVlc;
    private MediaPlayer? _mediaPlayer;
    private LibVlcPlaybackEngine? _playbackEngine;
    private PlayerViewModel? _viewModel;
    private bool _initialized;
    private bool _initializing;
    private int _attachmentGeneration;
    private readonly PlaybackWatchdog _bufferingWatchdog = new(TimeSpan.FromSeconds(12));
    private string? _activeStreamUrl;
    private readonly StreamFallbackQueue _fallbackStreams = new();
    private bool _fallbackAttempted;
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
        _playbackUiTimer.Tick += (_, _) => ReconcilePlayingUi();
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

                _mediaPlayer = mediaPlayer;
                _libVlc = libVlc;
                _playbackEngine = new LibVlcPlaybackEngine(libVlc, mediaPlayer);

                var videoView = this.FindControl<LibVLCSharp.Avalonia.VideoView>("VideoView")
                    ?? throw new InvalidOperationException("VideoView control was not found in PlayerView.");
                videoView.MediaPlayer = _mediaPlayer;
                _initialized = true;
                _initializing = false;
                Log("LibVLC and MediaPlayer initialized; VideoView assigned");
                PlayCurrentStream();
            });
        }
        catch (System.Exception ex)
        {
            mediaPlayer?.Dispose();
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
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
        ApplyPlaybackState(PlayerPlaybackCommandKind.Suspend);
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

        _unsubscribePlayback?.Invoke();
        _unsubscribePlayback = null;

        // NativeControlHost may destroy its HWND after this UserControl detaches.
        // Clear VideoView.MediaPlayer before disposing the native MediaPlayer;
        // otherwise VideoView.Detach() can call libvlc_media_player_set_hwnd
        // on an already-freed native pointer (0xC0000005).
        var videoView = this.FindControl<LibVLCSharp.Avalonia.VideoView>("VideoView");
        if (videoView != null)
        {
            videoView.MediaPlayer = null;
            Log("VideoView.MediaPlayer detached before disposal");
        }

        _playbackUiTimer.Stop();
        _playbackEngine?.DisposeAsync().AsTask().GetAwaiter().GetResult();
        _playbackEngine = null;
        _bufferingWatchdog.Cancel();
        _mediaPlayer?.Dispose();
        DisposeMasterVolume();

        _mediaPlayer = null;
        _libVlc = null;
        _activeStreamUrl = null;
        _fallbackAttempted = false;
        _initialized = false;
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
        ReconcilePlayingUi();
        if (_viewModel?.ChannelPickerVisible == true)
        {
            _overlayTimer.Stop();
            _overlayTimer.Start();
            return;
        }

        if (CanAutoHideControls)
            HideOverlay();
    }

    // Native callbacks and watchdogs run on the UI thread after token validation.
    private void ApplyPlaybackState(PlayerPlaybackCommandKind command)
    {
        _bufferingWatchdog.Cancel();
        _playbackUiTimer.Stop();
        ReportPlayback(command);
    }

    private bool ReportPlayback(PlayerPlaybackCommandKind command, float percent = 0)
    {
        if (_viewModel == null) return false;
        var model = _viewModel.Playback;
        var token = model.Snapshot.Token;
        return model.Apply(new(command, token.Generation, token.Epoch, Percent: percent));
    }

    private void ReconcilePlayingUi()
    {
        if (_viewModel == null || !_viewModel.Playback.CanStart(_activeStreamUrl ?? string.Empty) ||
            _mediaPlayer?.IsPlaying != true || _hasPlaybackStarted)
            return;

        ApplyPlaybackState(PlayerPlaybackCommandKind.Playing);
        _viewModel!.SetPlaybackStatus(string.Empty);
        if (_overlayVisible)
        {
            _overlayTimer.Stop();
            _overlayTimer.Start();
        }
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

        PlayCurrentStream();
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(PlayerViewModel.StreamUrl))
        {
            Log("ViewModel StreamUrl changed -> replaying stream");
            PlayCurrentStream();
        }
    }

    private void PlayCurrentStream()
    {
        if (!_initialized || _viewModel == null || _mediaPlayer == null || _libVlc == null)
        {
            Log($"PlayCurrentStream skipped: initialized={_initialized}, hasVm={_viewModel != null}, hasPlayer={_mediaPlayer != null}, hasLibVlc={_libVlc != null}");
            return;
        }

        if (string.IsNullOrWhiteSpace(_viewModel.StreamUrl))
        {
            _bufferingWatchdog.Cancel();
            _playbackUiTimer.Stop();
            _unsubscribePlayback?.Invoke();
            _unsubscribePlayback = null;
            _mediaPlayer.Stop();
            Log("PlayCurrentStream aborted: empty StreamUrl");
            return;
        }

        try
        {
            var streamUrl = _viewModel.StreamUrl.Trim();
            _viewModel.Playback.Apply(new(PlayerPlaybackCommandKind.Resume, _viewModel.Playback.Snapshot.Token.Generation));
            var token = _viewModel.Playback.Snapshot.Token;
            if (!_viewModel.Playback.Apply(new(PlayerPlaybackCommandKind.Connect, token.Generation, Url: streamUrl))) return;
            Log($"PlayCurrentStream start. Url={FormatUrlForLog(streamUrl)}");
            _playbackUiTimer.Start();
            ShowOverlay();
            _activeStreamUrl = streamUrl;
            _fallbackStreams.Reset(_viewModel.FallbackStreamUrls);
            _fallbackAttempted = false;

            _viewModel!.SetPlaybackStatus(string.Empty);
            ReportPlayback(PlayerPlaybackCommandKind.Buffering);
            StartBufferingWatchdog();

            var started = StartPlayback(streamUrl, fallback: false);
            Log($"Primary playback start result: {started}");
            if (!started && !TryStartNextStreamFallback())
            {
                ApplyPlaybackState(PlayerPlaybackCommandKind.Fail);
                _viewModel!.SetPlaybackStatus("Stream failed to start. Try another channel or Open Externally.");
            }
        }
        catch (System.Exception ex)
        {
            Log($"PlayCurrentStream exception: {ex}");
            ApplyPlaybackState(PlayerPlaybackCommandKind.Fail);
            _viewModel!.SetPlaybackStatus($"Embedded playback failed: {ex.Message}. Use Open Externally as a fallback.");
        }
    }

    private bool StartPlayback(string streamUrl, bool fallback)
    {
        if (_playbackEngine == null)
            return false;

        Log($"StartPlayback called. fallback={fallback}, url={FormatUrlForLog(streamUrl)}");
        try
        {
            BindPlaybackCallbacks();
            var source = new StreamSource(streamUrl);
            if (fallback)
                _playbackEngine.PlayFallbackAsync(source).GetAwaiter().GetResult();
            else
                _playbackEngine.PlayAsync(source).GetAwaiter().GetResult();
            return true;
        }
        catch (Exception ex)
        {
            Log($"StartPlayback failed: {ex}");
            return false;
        }
    }

    private void BindPlaybackCallbacks()
    {
        _unsubscribePlayback?.Invoke();
        var player = _mediaPlayer!;
        var model = _viewModel!.Playback;
        var token = model.Snapshot.Token;
        void Dispatch(Action action) => DispatchPlaybackCallback(model, token, player, action);
        EventHandler<MediaPlayerBufferingEventArgs> buffering = (sender, args) => Dispatch(() => OnMediaPlayerBuffering(sender, args));
        EventHandler<EventArgs> playing = (sender, args) => Dispatch(() => OnMediaPlayerPlaying(sender, args));
        EventHandler<EventArgs> error = (sender, args) => Dispatch(() => OnMediaPlayerEncounteredError(sender, args));
        EventHandler<EventArgs> ended = (sender, args) => Dispatch(() => OnMediaPlayerEndReached(sender, args));
        EventHandler<EventArgs> paused = (_, _) => Dispatch(() => ApplyPlaybackState(PlayerPlaybackCommandKind.Pause));
        player.Buffering += buffering;
        player.Playing += playing;
        player.EncounteredError += error;
        player.EndReached += ended;
        player.Paused += paused;
        _unsubscribePlayback = () =>
        {
            player.Buffering -= buffering;
            player.Playing -= playing;
            player.EncounteredError -= error;
            player.EndReached -= ended;
            player.Paused -= paused;
        };
    }

    private void StartBufferingWatchdog()
    {
        var model = _viewModel?.Playback;
        var attempt = model?.Snapshot.Token;
        var player = _mediaPlayer;
        Log("Buffering watchdog started (12s)");

        _bufferingWatchdog.Arm(token =>
        {
            Dispatcher.UIThread.Post(() =>
            {
                if (token.IsCancellationRequested ||
                    model == null || !ReferenceEquals(model, _viewModel?.Playback) ||
                    attempt == null || !model.IsCurrent(attempt.Value) || !ReferenceEquals(player, _mediaPlayer) ||
                    _mediaPlayer == null || _viewModel == null || string.IsNullOrWhiteSpace(_activeStreamUrl))
                    return;

                ReconcilePlayingUi();
                if (_mediaPlayer.IsPlaying)
                    return;

                if (TryStartNextStreamFallback())
                    return;

                if (_fallbackAttempted)
                    return;

                _fallbackAttempted = true;
                Log("Buffering watchdog triggered fallback attempt");
                _viewModel!.SetPlaybackStatus(string.Empty);
                ReportPlayback(PlayerPlaybackCommandKind.Recover);
                var started = StartPlayback(_activeStreamUrl!, fallback: true);
                Log($"Fallback playback start result: {started}");
                if (!started)
                {
                    ApplyPlaybackState(PlayerPlaybackCommandKind.Fail);
                    _viewModel!.SetPlaybackStatus("Fallback playback failed. Try Open Externally for this channel.");
                }
            });
        });
    }

    private bool TryStartNextStreamFallback()
    {
        if (_mediaPlayer == null || _viewModel == null || !_fallbackStreams.TryNext(out var fallbackUrl))
            return false;
        _activeStreamUrl = fallbackUrl;
        ReportPlayback(PlayerPlaybackCommandKind.Recover);
        _viewModel.Playback.Apply(new(PlayerPlaybackCommandKind.Connect, _viewModel.Playback.Snapshot.Token.Generation, Url: fallbackUrl));
        _playbackUiTimer.Start();
        ShowOverlay();
        _fallbackAttempted = false;
        Log($"Trying alternate stream URL {_fallbackStreams.Attempted}/{_fallbackStreams.Total}: {FormatUrlForLog(fallbackUrl)}");

        _viewModel!.SetPlaybackStatus("Trying alternate stream…");
        StartBufferingWatchdog();

        var started = StartPlayback(fallbackUrl, fallback: false);
        Log($"Alternate stream start result: {started}");
        return started;
    }

    // The binding captures the attempt before native playback starts, rather than
    // reading the current channel when a late native event happens to arrive.
    private void DispatchPlaybackCallback(PlayerPlaybackModel model, PlaybackToken attempt, object player, Action action)
    {
        Dispatcher.UIThread.Post(() =>
        {
            if (!ReferenceEquals(model, _viewModel?.Playback) || !model.IsCurrent(attempt) ||
                !ReferenceEquals(player, _mediaPlayer) || !model.CanStart(_activeStreamUrl ?? string.Empty)) return;
            action();
        });
    }

    private void OnMediaPlayerBuffering(object? sender, MediaPlayerBufferingEventArgs e)
    {
        if (_viewModel == null)
            return;

        if (e.Cache < 1 || ((int)e.Cache % 10) == 0)
        {
            Log($"MediaPlayer buffering: {e.Cache:0}%");
        }

        ReconcilePlayingUi();
        if (_viewModel != null)
        {
            ReportPlayback(PlayerPlaybackCommandKind.Buffering, e.Cache);
        }
    }

    private void OnMediaPlayerPlaying(object? sender, System.EventArgs e)
    {
        Log($"MediaPlayer event: Playing. audioTracks={_mediaPlayer?.AudioTrackCount}, audioTrack={_mediaPlayer?.AudioTrack}, volume={_mediaPlayer?.Volume}, muted={_mediaPlayer?.Mute}");
        ApplyPlaybackState(PlayerPlaybackCommandKind.Playing);
        ShowOverlay();
        _viewModel!.SetPlaybackStatus(string.Empty);
    }

    private void OnMediaPlayerEncounteredError(object? sender, System.EventArgs e)
    {
        Log("MediaPlayer event: EncounteredError");
        if (TryStartNextStreamFallback())
            return;

        ApplyPlaybackState(PlayerPlaybackCommandKind.Fail);
        ShowOverlay();
        _viewModel!.SetPlaybackStatus("Playback error. The stream may be unavailable or unsupported.");
    }

    private void OnMediaPlayerEndReached(object? sender, System.EventArgs e)
    {
        Log("MediaPlayer event: EndReached");
        ApplyPlaybackState(PlayerPlaybackCommandKind.End);
        ShowOverlay();
        _viewModel!.SetPlaybackStatus("Stream ended.");
    }

    private static string FormatUrlForLog(string url)
    {
        if (string.IsNullOrWhiteSpace(url))
            return "<empty>";

        return url.Trim();
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
