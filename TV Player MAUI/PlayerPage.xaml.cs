using LibVLCSharp.Shared;
using TV_Player;
using TV_Player.Presentation;
using TV_Player.Input;
#if ANDROID
using Microsoft.Maui.ApplicationModel;
#endif

namespace TV_Player.Maui;

public partial class PlayerPage : ContentPage
{
    private readonly MauiPlaybackController _playback;
    private readonly MauiPlayerUiController _ui;
    private readonly ProgramGuideController _guideController;
    private readonly StreamResolutionController _streamResolver;
    private bool _isClosing;
    private bool _pageVisible;
    private bool _appActive = true;
#if ANDROID
    private LibVLCSharp.Platforms.Android.VideoView? _nativeVideoView;
    private VideoSurfaceCallback? _surfaceCallback;
    private Android.Views.ISurfaceHolder? _surfaceHolder;
#endif
    private readonly ChannelSelectionState _selection;

    public PlayerPage(M3UInfo channel, IReadOnlyList<M3UInfo> channels, ProgramsData sourceData)
    {
        InitializeComponent();
        _ui = new MauiPlayerUiController(Dispatcher, RenderPlayerUi);
        PlayerLayout.SizeChanged += (_, _) => RefreshVideoSurfaceSize();
        _streamResolver = new StreamResolutionController(sourceData);
        _playback = new MauiPlaybackController(BindPlayback, DetachPlayback, () => OnMediaPlayerPlaying(null, EventArgs.Empty), OnPlaybackError);
        _guideController = new ProgramGuideController(sourceData, ResetProgramGuide, DisplayProgramGuide, RefreshProgramGuide);
        _selection = new ChannelSelectionState(channel);
        _selection.SetChannels(channels);
        _selection.ChannelRequested += PlayChannel;
        ChannelPicker.ChannelSelected += selected =>
        {
            _ui.CloseOverlay();
            _selection.Select(selected);
        };
        ChannelPicker.SetChannels(_selection.Channels);
        ChannelPicker.SelectChannel(channel);
        ChannelTitle.Text = channel.Name;
        ClockLabel.Text = DateTime.Now.ToString("HH:mm");
        PlayChannel(channel);
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        _pageVisible = true;
        _appActive = true;
        PrivacyOverlay.IsVisible = false;
#if ANDROID
        MainActivity.PlayerKeyPressed += OnRemoteKeyPressed;
        MainActivity.AppPausing += OnApplicationPausing;
        MainActivity.AppResumed += OnApplicationResumed;
        try
        {
            if (Platform.CurrentActivity is MainActivity activity)
            {
                activity.SetImmersiveMode(true);
                activity.FocusPlayerSurface();
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to enter immersive mode: {ex.Message}");
        }
#endif
        RefreshVideoSurfaceSize();
        _ui.Resume();
        RestoreRemoteFocus();
    }

    private void OnPlayerLoaded(object? sender, EventArgs e) => RefreshVideoSurfaceSize();
    private void OnVideoHandlerChanged(object? sender, EventArgs e) => RefreshVideoSurfaceSize();
    private void OnVideoSizeChanged(object? sender, EventArgs e) => RefreshVideoSurfaceSize();

    private void OnPlayerUnloaded(object? sender, EventArgs e)
    {
        _playback?.SetSurfaceReady(false);
        DetachSurfaceCallback();
    }

    private void OnMediaPlayerPlaying(object? sender, EventArgs e) => MainThread.BeginInvokeOnMainThread(() =>
    {
        if (_isClosing || !_pageVisible || !_appActive) return;
        RefreshVideoSurfaceSize();
        StatusLabel.Text = string.Empty;
    });

    private void OnPlaybackError() => MainThread.BeginInvokeOnMainThread(() =>
    {
        if (_isClosing || !_pageVisible || !_appActive) return;
        PrivacyOverlay.IsVisible = false;
        StatusLabel.Text = "VLC не смог открыть поток. Попробуйте другой канал или откройте этот канал заново.";
        _ui.Show();
    });

    private void RefreshVideoSurfaceSize()
    {
        if (_playback == null || _isClosing) return;
#if ANDROID
        var native = VideoPlayer.Handler?.PlatformView as LibVLCSharp.Platforms.Android.VideoView;
        if (!ReferenceEquals(native, _nativeVideoView))
        {
            _playback.SetSurfaceReady(false);
            DetachSurfaceCallback();
            _nativeVideoView = native;
            if (native != null)
            {
                _surfaceCallback = new VideoSurfaceCallback(() => MainThread.BeginInvokeOnMainThread(RefreshVideoSurfaceSize),
                    () => _playback.SetSurfaceReady(false));
                _surfaceHolder = native.Holder;
                _surfaceHolder?.AddCallback(_surfaceCallback);
            }
        }
        var ready = _pageVisible && _appActive && native != null && native.Width > 0 && native.Height > 0 &&
            native.Holder?.Surface?.IsValid == true;
        if (ready) native!.TriggerLayoutChangeListener();
        try { _playback.SetSurfaceReady(ready); }
        catch (Exception ex) { StatusLabel.Text = $"Не удалось запустить видео: {ex.Message}"; _ui.Show(); }
#endif
    }

    private void DetachSurfaceCallback()
    {
#if ANDROID
        if (_surfaceHolder != null && _surfaceCallback != null)
            _surfaceHolder.RemoveCallback(_surfaceCallback);
        _surfaceHolder = null;
        _nativeVideoView = null;
        _surfaceCallback?.Dispose();
        _surfaceCallback = null;
#endif
    }

#if ANDROID
    private sealed class VideoSurfaceCallback(Action changed, Action destroyed) : Java.Lang.Object, Android.Views.ISurfaceHolderCallback
    {
        public void SurfaceCreated(Android.Views.ISurfaceHolder holder) => changed();
        public void SurfaceChanged(Android.Views.ISurfaceHolder holder, Android.Graphics.Format format, int width, int height) => changed();
        public void SurfaceDestroyed(Android.Views.ISurfaceHolder holder) => destroyed();
    }
#endif

    private async void PlayChannel(M3UInfo channel)
    {
        if (_isClosing) return;

        _selection.Sync(channel);
        ChannelPicker.SelectChannel(channel);

        _ = _guideController.LoadAsync(channel);
        var result = await _streamResolver.ResolveAsync(channel, () => StatusLabel.Text = "Получение потока…");
        if (result == null || _isClosing) return;
        if (result.Error != null)
        {
            StatusLabel.Text = result.Error;
            return;
        }
        var streamUrl = result.Url!;
        ChannelTitle.Text = channel.Name;
        StatusLabel.Text = "Подключение…";
        try { _playback.Play(streamUrl); }
        catch (Exception ex) { StatusLabel.Text = $"Не удалось запустить видео: {ex.Message}"; }
        _ui.Show();
    }

    private async void OnBackClicked(object sender, EventArgs e) => await LeavePlayerAsync();

    protected override bool OnBackButtonPressed()
    {
        if (!_ui.CloseOverlay()) _ = LeavePlayerAsync();
        return true;
    }

    private async Task LeavePlayerAsync()
    {
        if (_isClosing) return;
        _isClosing = true;
        _ui.Suspend();
        ChannelPicker.CancelPendingFocus();
        ProgramGuideList.CancelPendingFocus();
        _guideController.Cancel();
        _streamResolver.Cancel();

        _playback.SetSurfaceReady(false);
        _playback.StopAndDetach();
        DetachSurfaceCallback();

        try
        {
            if (Navigation.NavigationStack.Count > 1)
                await Navigation.PopAsync();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to leave player page: {ex}");
        }
        finally
        {
            _playback.Dispose();
            _streamResolver.Dispose();
            _guideController.Dispose();
            _ui.Dispose();
        }
    }

    private void OnVideoTapped(object? sender, TappedEventArgs e) => _ui.ToggleControls();

    private async void OnPickerClicked(object sender, EventArgs e)
    {
        _ui.TogglePicker();
        if (_ui.State.ChannelPickerVisible)
            await ChannelPicker.ScrollSelectedChannelIntoViewAsync();
    }

    private void OnGuideInfoTapped(object? sender, TappedEventArgs e) => _ui.ToggleGuide();

    private void RenderPlayerUi(PlayerUiState state)
    {
        PlayerTopBar.IsVisible = state.ControlsVisible;
        PlayerBottomBar.IsVisible = state.ControlsVisible;
        var pickerOpened = state.ChannelPickerVisible && !ChannelPicker.IsVisible;
        var guideOpened = state.ProgramGuideVisible && !ProgramGuideList.IsVisible;
        ChannelPicker.IsVisible = state.ChannelPickerVisible;
        ProgramGuideList.IsVisible = state.ProgramGuideVisible;
        if (!state.ChannelPickerVisible) ChannelPicker.CancelPendingFocus();
        if (!state.ProgramGuideVisible) ProgramGuideList.CancelPendingFocus();
        if (pickerOpened && IsRemoteNavigation) _ = ChannelPicker.FocusSelectedChannelAsync();
        else if (guideOpened && IsRemoteNavigation) ProgramGuideList.FocusFirstProgram();
        else if (!state.ChannelPickerVisible && !state.ProgramGuideVisible) FocusPlayerSurface();
    }

    private bool OnRemoteKeyPressed(PlayerRemoteKey key, bool nativeControlFocused)
    {
        if (_isClosing) return false;
        var context = PlayerInputRouter.GetContext(_ui.State.ProgramGuideVisible, _ui.State.ChannelPickerVisible);
        var result = PlayerRemoteInputRouter.Route(key, context, nativeControlFocused);
        switch (result.Action)
        {
            case PlayerRemoteAction.None: return false;
            case PlayerRemoteAction.SwitchChannel: _selection.Switch(result.Direction); break;
            case PlayerRemoteAction.MovePicker: _ = ChannelPicker.MoveFocusAsync(result.Direction); break;
            case PlayerRemoteAction.MoveGuide: _ = ProgramGuideList.MoveFocusAsync(result.Direction); break;
            case PlayerRemoteAction.OpenPicker: _ui.TogglePicker(); break;
            case PlayerRemoteAction.OpenGuide:
                if (ProgramInfoPanel.IsVisible) _ui.ToggleGuide(); else _ui.Show();
                break;
            case PlayerRemoteAction.CloseOverlay: _ui.CloseOverlay(); break;
            case PlayerRemoteAction.LeavePlayer: _ = LeavePlayerAsync(); break;
        }
        return true;
    }

    private void RestoreRemoteFocus()
    {
        if (!IsRemoteNavigation) return;
        if (_ui.State.ChannelPickerVisible) _ = ChannelPicker.FocusSelectedChannelAsync();
        else if (_ui.State.ProgramGuideVisible) ProgramGuideList.FocusFirstProgram();
        else FocusPlayerSurface();
    }

    private static bool IsRemoteNavigation
    {
        get
        {
#if ANDROID
            return Platform.CurrentActivity is MainActivity activity && activity.IsRemoteNavigation;
#else
            return false;
#endif
        }
    }

    private static void FocusPlayerSurface()
    {
#if ANDROID
        if (Platform.CurrentActivity is MainActivity activity) activity.FocusPlayerSurface();
#endif
    }

    private void ResetProgramGuide()
    {
        ProgramInfoPanel.IsVisible = false;
        _ui.CloseGuide();
        ProgramGuideList.Clear();
    }

    private void DisplayProgramGuide(ProgramGuide guide)
    {
        var ordered = guide.Programs.OrderBy(item => item.StartTime).ToList();
        var now = DateTime.Now;
        var first = ordered.FindIndex(item => item.StartTime <= now && now < item.EndTime);
        if (first < 0) first = ordered.FindIndex(item => item.StartTime > now);
        if (first < 0) first = 0;
        ProgramGuideList.SetPrograms(ordered.Skip(first).Take(7));
        RefreshProgramGuide(guide);
    }

    private void RefreshProgramGuide(ProgramGuide guide)
    {
        UpdateProgramInfo(guide);
        ProgramGuideList.Refresh();
    }

    private void UpdateProgramInfo(ProgramGuide guide)
    {
        var now = DateTime.Now;
        var programs = guide.Programs.OrderBy(item => item.StartTime).ToList();
        var current = programs.FirstOrDefault(item => item.StartTime <= now && item.EndTime >= now);
        var next = current == null ? programs.FirstOrDefault(item => item.StartTime > now) : current;
        if (next == null)
        {
            ProgramInfoPanel.IsVisible = false;
            return;
        }

        ProgramInfoPanel.IsVisible = true;
        ProgramTitleLabel.Text = current == null ? $"Далее: {next.Title}" : next.Title;
        ProgramStartLabel.Text = next.StartTime.ToString("HH:mm");
        ProgramEndLabel.Text = next.EndTime.ToString("HH:mm");
        var duration = (next.EndTime - next.StartTime).TotalSeconds;
        ProgramProgress.Progress = current == null || duration <= 0
            ? 0
            : Math.Clamp((now - next.StartTime).TotalSeconds / duration, 0, 1);
    }

    protected override void OnDisappearing()
    {
        _pageVisible = false;
        _playback.SetSurfaceReady(false);
        DetachSurfaceCallback();
        base.OnDisappearing();
#if ANDROID
        MainActivity.PlayerKeyPressed -= OnRemoteKeyPressed;
        MainActivity.AppPausing -= OnApplicationPausing;
        MainActivity.AppResumed -= OnApplicationResumed;
#endif
        _ui.Suspend();
        ChannelPicker.CancelPendingFocus();
        ProgramGuideList.CancelPendingFocus();
        _guideController.Cancel();
#if ANDROID
        try
        {
            if (Platform.CurrentActivity is MainActivity activity)
                activity.SetImmersiveMode(false);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to restore system bars: {ex.Message}");
        }
#endif
    }

    private void OnApplicationPausing()
    {
        _appActive = false;
        _ui.Suspend();
        ChannelPicker.CancelPendingFocus();
        ProgramGuideList.CancelPendingFocus();
        _playback.PauseForAppSwitch();
        PrivacyOverlay.IsVisible = true;
    }

    private void OnApplicationResumed()
    {
        _appActive = true;
        RefreshVideoSurfaceSize();
        if (!_isClosing)
        {
            _ui.Resume();
            RestoreRemoteFocus();
        }
        if (!_playback.ShouldResume || _isClosing)
        {
            PrivacyOverlay.IsVisible = false;
            return;
        }
        _ = ResumePlaybackAfterAppSwitchAsync();
    }

    private async Task ResumePlaybackAfterAppSwitchAsync()
    {
        try
        {
            await _playback.ResumeAfterAppSwitchAsync(CancellationToken.None);
            if (_isClosing) return;
            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                RefreshVideoSurfaceSize();
                PrivacyOverlay.IsVisible = false;
            });
        }
        catch (OperationCanceledException) { }
        catch (Exception ex) { StatusLabel.Text = $"Не удалось восстановить видео: {ex.Message}"; }
        finally { if (!_isClosing && _appActive) PrivacyOverlay.IsVisible = false; }
    }

    private void BindPlayback(MediaPlayer player)
    {
        VideoPlayer.MediaPlayer = player;
#if ANDROID
        // Attach creates a new VLC AWindow after the Android layout may already be complete.
        // Supply its size before Play; waiting for another layout event can leave video black.
        _nativeVideoView?.TriggerLayoutChangeListener();
#endif
    }

    private void DetachPlayback(MediaPlayer? player)
    {
        VideoPlayer.MediaPlayer = null;
        if (player != null)
        {
            VideoPlayer.MediaPlayer = player;
#if ANDROID
            _nativeVideoView?.TriggerLayoutChangeListener();
#endif
        }
    }
}
