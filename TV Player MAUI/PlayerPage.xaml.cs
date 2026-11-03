using TV_Player.Playback;
using LibVLCSharp.Shared;
using TV_Player;
using TV_Player.Providers;
using TV_Player.Presentation;
using TV_Player.Input;
using System.Reactive.Linq;
#if ANDROID
using Microsoft.Maui.ApplicationModel;
#endif

namespace TV_Player.Maui;

public partial class PlayerPage : ContentPage
{
    private readonly PlayerPresentationModel _presentation;
    private PlayerPlaybackModel _playbackModel => _presentation.Playback;
    private readonly IDispatcherTimer _guideTimer;
    private readonly IDisposable _guideSubscription;
    private long _renderedChannelVersion;
    private ProgramGuideState? _renderedGuide;
    private readonly MauiPlaybackController _playback;
    private readonly MauiPlayerUiController _ui;
    private bool _isClosing;
    private bool _pageVisible;
    private bool _appActive = true;
#if ANDROID
    private LibVLCSharp.Platforms.Android.VideoView? _nativeVideoView;
    private VideoSurfaceCallback? _surfaceCallback;
    private Android.Views.ISurfaceHolder? _surfaceHolder;
#endif

    public PlayerPage(M3UInfo channel, IReadOnlyList<M3UInfo> channels, ProgramsData sourceData)
    {
        InitializeComponent();
        _ui = new MauiPlayerUiController(Dispatcher, RenderPlayerUi);
        PlayerLayout.SizeChanged += (_, _) => RefreshVideoSurfaceSize();
        _presentation = new PlayerPresentationModel(new StreamResolutionService(sourceData.Provider),
            async selected => await sourceData.GetGuideByProgram(selected), MainThread.InvokeOnMainThreadAsync,
            formatProvider: System.Globalization.CultureInfo.CurrentCulture);
        _playback = new MauiPlaybackController(BindPlayback, DetachPlayback, _playbackModel, MainThread.BeginInvokeOnMainThread);
        _presentation.Changed += RenderPresentation;
        _presentation.StreamRequested += PlayStream;
        _presentation.SetChannels(channels);
        _guideTimer = Dispatcher.CreateTimer();
        _guideTimer.Interval = TimeSpan.FromMinutes(1);
        _guideTimer.Tick += OnGuideTick;
        _guideSubscription = sourceData.ProgramGuideInfo.Subscribe(signal => MainThread.BeginInvokeOnMainThread(() =>
        {
            if (!_isClosing) _ = _presentation.NotifyGuideAvailableAsync();
        }));
        ChannelPicker.ChannelSelected += selected =>
        {
            _ui.CloseOverlay();
            _presentation.SelectChannel(selected);
        };
        ChannelPicker.SetChannels(_presentation.Selection.Channels);
        ClockLabel.Text = DateTime.Now.ToString("HH:mm");
        _ = _presentation.OpenAsync(channel);
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        _pageVisible = true;
        _guideTimer.Start();
        _presentation.RefreshGuide();
#if ANDROID
        _appActive = MainActivity.IsAppActive;
#else
        _appActive = true;
#endif
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
        _playback.SetForeground(_appActive);
        RefreshVideoSurfaceSize();
        if (_appActive)
        {
            _ui.Resume();
            RestoreRemoteFocus();
        }
    }

    private void OnPlayerLoaded(object? sender, EventArgs e) => RefreshVideoSurfaceSize();
    private void OnVideoHandlerChanged(object? sender, EventArgs e) => RefreshVideoSurfaceSize();
    private void OnVideoSizeChanged(object? sender, EventArgs e) => RefreshVideoSurfaceSize();

    private void OnPlayerUnloaded(object? sender, EventArgs e)
    {
        _playback?.SetSurfaceReady(false);
        DetachSurfaceCallback();
    }

    private void OnGuideTick(object? sender, EventArgs e) => _presentation.RefreshGuide();

    private void RenderPresentation(PlayerPresentationSnapshot state)
    {
        if (_isClosing) return;
        ChannelTitle.Text = state.ChannelTitle;
        if (state.ChannelVersion != _renderedChannelVersion)
        {
            _renderedChannelVersion = state.ChannelVersion;
            if (state.Channel != null) ChannelPicker.SelectChannel(state.Channel);
            ResetProgramGuide();
        }
        if (!ReferenceEquals(_renderedGuide, state.Guide))
        {
            _renderedGuide = state.Guide;
            RenderProgramGuide(state);
        }
        RenderPlayback(state.Playback, state.Status);
    }

    private void RenderPlayback(PlayerPlaybackSnapshot state, PlayerStatus status)
    {
        if (_isClosing) return;
        StatusLabel.Text = status switch
        {
            PlayerStatus.Resolving => "Получение потока…",
            PlayerStatus.Connecting => "Подключение…",
            PlayerStatus.NoStream => "Для этого канала нет ссылки на поток.",
            PlayerStatus.ResolutionFailed => "Не удалось получить поток. Попробуйте открыть канал заново.",
            PlayerStatus.PlaybackFailed or PlayerStatus.EngineUnavailable => "VLC не смог открыть поток. Попробуйте другой канал или откройте этот канал заново.",
            PlayerStatus.Ended => "Поток завершён.",
            _ => string.Empty
        };
        if (state.Phase == PlayerPlaybackPhase.Playing && _pageVisible && _appActive) RefreshVideoSurfaceSize();
        if (state.Phase is PlayerPlaybackPhase.Error or PlayerPlaybackPhase.Ended) _ui.Show();
    }

    private void ReportStartupFailure(Exception error)
    {
        var token = _playbackModel.Snapshot.Token;
        _playbackModel.Apply(new(PlayerPlaybackCommandKind.Fail, token.Generation, token.Epoch, Cause: error));
    }

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
        catch (Exception ex) { ReportStartupFailure(ex); }
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

    private void PlayStream(ResolvedStream stream)
    {
        if (_isClosing) return;
        try { _playback.Play(stream.Url); }
        catch (Exception ex) { ReportStartupFailure(ex); }
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
        _presentation.CancelPending();
        _guideTimer.Stop();

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
            _guideTimer.Tick -= OnGuideTick;
            _guideSubscription.Dispose();
            _presentation.Changed -= RenderPresentation;
            _presentation.StreamRequested -= PlayStream;
            _presentation.Dispose();
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
            case PlayerRemoteAction.SwitchChannel: _presentation.SwitchChannel(result.Direction); break;
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

    private void RenderProgramGuide(PlayerPresentationSnapshot state)
    {
        ProgramGuideList.SetPrograms(state.Guide.Window);
        var program = state.Program;
        ProgramInfoPanel.IsVisible = program != null;
        if (program == null)
        {
            ProgramTitleLabel.Text = ProgramStartLabel.Text = ProgramEndLabel.Text = string.Empty;
            ProgramProgress.Progress = 0;
            return;
        }
        ProgramTitleLabel.Text = state.IsNextProgram ? $"Далее: {program.Title}" : program.Title;
        ProgramStartLabel.Text = program.TimeLabels.Start;
        ProgramEndLabel.Text = program.TimeLabels.End;
        ProgramProgress.Progress = program.Progress;
    }

    protected override void OnDisappearing()
    {
        _pageVisible = false;
        _playback.SetForeground(false);
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
        _guideTimer.Stop();
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
        _playback.SetForeground(false);
        PrivacyOverlay.IsVisible = true;
    }

    private void OnApplicationResumed()
    {
        _appActive = true;
        if (_isClosing || !_pageVisible) return;
        PrivacyOverlay.IsVisible = false;
        _playback.SetForeground(true);
        _ui.Resume();
        RestoreRemoteFocus();
        RefreshVideoSurfaceSize();
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

    private void DetachPlayback() => VideoPlayer.MediaPlayer = null;
}
