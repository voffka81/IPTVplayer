using LibVLCSharp.Shared;
using TV_Player;
#if ANDROID
using Microsoft.Maui.ApplicationModel;
#endif

namespace TV_Player.Maui;

public partial class PlayerPage : ContentPage
{
    private readonly MauiPlaybackController _playback;
    private readonly ProgramsData _sourceData;
    private CancellationTokenSource? _hideUiToken;
    private readonly ProgramGuideController _guideController;
    private CancellationTokenSource? _playbackRequestToken;
    private bool _isClosing;
    private M3UInfo? _selectedChannel;

    public PlayerPage(M3UInfo channel, IReadOnlyList<M3UInfo> channels, ProgramsData sourceData)
    {
        InitializeComponent();
        PlayerLayout.SizeChanged += (_, _) => RefreshVideoSurfaceSize();
        _sourceData = sourceData;
        _playback = new MauiPlaybackController(BindPlayback, DetachPlayback, () => OnMediaPlayerPlaying(null, EventArgs.Empty));
        _guideController = new ProgramGuideController(sourceData, ResetProgramGuide, DisplayProgramGuide, RefreshProgramGuide);
        _selectedChannel = channel;
        ChannelPicker.ChannelSelected += selected => PlayChannel(selected);
        ChannelPicker.SetChannels(channels);
        ChannelPicker.SelectChannel(channel);
        ChannelTitle.Text = channel.Name;
        ClockLabel.Text = DateTime.Now.ToString("HH:mm");
        PlayChannel(channel);
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        PrivacyOverlay.IsVisible = false;
#if ANDROID
        MainActivity.AppPausing += OnApplicationPausing;
        MainActivity.AppResumed += OnApplicationResumed;
        try
        {
            if (Platform.CurrentActivity is MainActivity activity)
                activity.SetImmersiveMode(true);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to enter immersive mode: {ex.Message}");
        }
#endif
        HideLibVlcControls();
        ShowPlayerUi();
    }

    private void OnPlayerLoaded(object? sender, EventArgs e)
    {
        HideLibVlcControls();
        RefreshVideoSurfaceSize();
    }

    private void OnMediaPlayerPlaying(object? sender, EventArgs e)
    {
#if ANDROID
        _ = RefreshVideoSurfaceSizeAfterPlaybackAsync();
#endif
    }

    private async Task RefreshVideoSurfaceSizeAfterPlaybackAsync()
    {
        await Task.Delay(150);
        await MainThread.InvokeOnMainThreadAsync(RefreshVideoSurfaceSize);
    }

    private void RefreshVideoSurfaceSize()
    {
#if ANDROID
        if (VideoPlayer.VideoView?.Handler?.PlatformView is LibVLCSharp.Platforms.Android.VideoView nativeVideoView &&
            nativeVideoView.Width > 0 && nativeVideoView.Height > 0)
        {
            nativeVideoView.TriggerLayoutChangeListener();
        }
#endif
    }

    private void HideLibVlcControls()
    {
        if (VideoPlayer.PlaybackControls != null)
            VideoPlayer.PlaybackControls.IsVisible = false;
    }

    private async void PlayChannel(M3UInfo channel)
    {
        if (_isClosing) return;

        _selectedChannel = channel;
        ChannelPicker.SelectChannel(channel);

        _playbackRequestToken?.Cancel();
        _playbackRequestToken?.Dispose();
        _playbackRequestToken = new CancellationTokenSource();
        var playbackToken = _playbackRequestToken.Token;

        _ = _guideController.LoadAsync(channel);
        var streamUrl = channel.Url;
        if (string.IsNullOrWhiteSpace(streamUrl) &&
            channel.ProviderId == _sourceData.Provider.Id &&
            !string.IsNullOrWhiteSpace(channel.ChannelId))
        {
            StatusLabel.Text = "Получение потока…";
            try
            {
                var resolved = await _sourceData.Provider.ResolveStreamAsync(channel.ChannelId, playbackToken);
                if (playbackToken.IsCancellationRequested || _isClosing) return;
                streamUrl = resolved.PrimaryUrl;
            }
            catch (OperationCanceledException) when (playbackToken.IsCancellationRequested) { return; }
            catch (Exception ex)
            {
                if (playbackToken.IsCancellationRequested || _isClosing) return;
                StatusLabel.Text = $"Не удалось получить поток: {ex.Message}";
                return;
            }
        }
        if (playbackToken.IsCancellationRequested || _isClosing) return;
        if (string.IsNullOrWhiteSpace(streamUrl))
        {
            StatusLabel.Text = "Для этого канала нет ссылки на поток.";
            return;
        }
        ChannelTitle.Text = channel.Name;
        StatusLabel.Text = "Подключение…";
        _playback.Play(streamUrl);
        StatusLabel.Text = string.Empty;
        ShowPlayerUi();
    }

    private async void OnBackClicked(object sender, EventArgs e) => await LeavePlayerAsync();

    protected override bool OnBackButtonPressed()
    {
        _ = LeavePlayerAsync();
        return true;
    }

    private async Task LeavePlayerAsync()
    {
        if (_isClosing) return;
        _isClosing = true;
        _hideUiToken?.Cancel();
        _guideController.Cancel();
        _playbackRequestToken?.Cancel();

        _playback.StopAndDetach();

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
        }
    }

    private void OnVideoTapped(object? sender, TappedEventArgs e)
    {
        if (PlayerTopBar.IsVisible || PlayerBottomBar.IsVisible)
        {
            _hideUiToken?.Cancel();
            ChannelPicker.IsVisible = false;
            ProgramGuideList.IsVisible = false;
            PlayerTopBar.IsVisible = false;
            PlayerBottomBar.IsVisible = false;
        }
        else
        {
            ShowPlayerUi();
        }
    }

    private async void OnPickerClicked(object sender, EventArgs e)
    {
        ChannelPicker.IsVisible = !ChannelPicker.IsVisible;
        ProgramGuideList.IsVisible = false;
        ShowPlayerUi();
        if (ChannelPicker.IsVisible)
            await ChannelPicker.ScrollSelectedChannelIntoViewAsync();
    }

    private void OnGuideInfoTapped(object? sender, TappedEventArgs e)
    {
        ProgramGuideList.IsVisible = !ProgramGuideList.IsVisible;
        ChannelPicker.IsVisible = false;
        ShowPlayerUi();
    }

    private void ResetProgramGuide()
    {
        ProgramInfoPanel.IsVisible = false;
        ProgramGuideList.IsVisible = false;
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

    private void ShowPlayerUi()
    {
        _hideUiToken?.Cancel();
        _hideUiToken?.Dispose();
        _hideUiToken = new CancellationTokenSource();
        PlayerTopBar.IsVisible = true;
        PlayerBottomBar.IsVisible = true;
        var token = _hideUiToken.Token;
        _ = HidePlayerUiAfterDelayAsync(token);
    }

    private async Task HidePlayerUiAfterDelayAsync(CancellationToken token)
    {
        try
        {
            await Task.Delay(TimeSpan.FromSeconds(4), token);
            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                if (!token.IsCancellationRequested && !ChannelPicker.IsVisible && !ProgramGuideList.IsVisible)
                {
                    PlayerTopBar.IsVisible = false;
                    PlayerBottomBar.IsVisible = false;
                }
            });
        }
        catch (OperationCanceledException) { }
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
#if ANDROID
        MainActivity.AppPausing -= OnApplicationPausing;
        MainActivity.AppResumed -= OnApplicationResumed;
#endif
        _hideUiToken?.Cancel();
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
        _playback.PauseForAppSwitch();
        PrivacyOverlay.IsVisible = true;
    }

    private void OnApplicationResumed()
    {
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
            await _playback.ResumeAfterAppSwitchAsync(_playbackRequestToken?.Token ?? CancellationToken.None);
            if (_isClosing) return;
            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                RefreshVideoSurfaceSize();
                PrivacyOverlay.IsVisible = false;
            });
        }
        catch (OperationCanceledException) { }
    }

    private void BindPlayback(LibVLC libVlc, MediaPlayer player)
    {
        VideoPlayer.LibVLC = libVlc;
        VideoPlayer.MediaPlayer = player;
    }

    private void DetachPlayback(MediaPlayer? player)
    {
        if (VideoPlayer.VideoView is { } view)
        {
            view.MediaPlayer = null;
            if (player != null) view.MediaPlayer = player;
        }
    }
}
