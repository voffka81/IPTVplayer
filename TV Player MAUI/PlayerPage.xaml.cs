using LibVLCSharp.Shared;
using TV_Player;
#if ANDROID
using Microsoft.Maui.ApplicationModel;
#endif

namespace TV_Player.Maui;

public partial class PlayerPage : ContentPage
{
    private LibVLC? _libVlc;
    private MediaPlayer? _mediaPlayer;
    private Media? _currentMedia;
    private readonly ProgramsData _sourceData;
    private CancellationTokenSource? _hideUiToken;
    private readonly ProgramGuideController _guideController;
    private CancellationTokenSource? _playbackRequestToken;
    private bool _isClosing;
    private bool _wasPlayingBeforeAppSwitch;
    private M3UInfo? _selectedChannel;

    public PlayerPage(M3UInfo channel, IReadOnlyList<M3UInfo> channels, ProgramsData sourceData)
    {
        InitializeComponent();
        PlayerLayout.SizeChanged += (_, _) => RefreshVideoSurfaceSize();
        _sourceData = sourceData;
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
        _libVlc ??= new LibVLC(enableDebugLogs: false);
        _mediaPlayer ??= new MediaPlayer(_libVlc);
        _mediaPlayer.Playing -= OnMediaPlayerPlaying;
        _mediaPlayer.Playing += OnMediaPlayerPlaying;
        VideoPlayer.LibVLC = _libVlc;
        VideoPlayer.MediaPlayer = _mediaPlayer;
        _currentMedia?.Dispose();
        _currentMedia = CreateMedia(streamUrl);
        _mediaPlayer.Play(_currentMedia);
        StatusLabel.Text = string.Empty;
        ShowPlayerUi();
    }

    private Media CreateMedia(string rawUrl)
    {
        var separator = rawUrl.IndexOf('|');
        var location = separator > 0 ? rawUrl[..separator].Trim() : rawUrl.Trim();
        var media = new Media(_libVlc!, location, FromType.FromLocation);
        media.AddOption(":network-caching=1500");
        media.AddOption(":live-caching=1500");
        media.AddOption(":http-reconnect=true");

        if (separator > 0)
        {
            foreach (var option in rawUrl[(separator + 1)..].Split('&', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                var equals = option.IndexOf('=');
                if (equals <= 0 || equals == option.Length - 1) continue;
                var key = option[..equals].Trim().ToLowerInvariant();
                var value = Uri.UnescapeDataString(option[(equals + 1)..].Trim());
                if (string.IsNullOrWhiteSpace(value)) continue;
                switch (key)
                {
                    case "user-agent": media.AddOption($":http-user-agent={value}"); break;
                    case "referer":
                    case "referrer":
                    case "http-referrer":
                    case "http-referer": media.AddOption($":http-referrer={value}"); break;
                    case "origin": media.AddOption($":http-header=Origin={value}"); break;
                    case "cookie":
                    case "cookies": media.AddOption($":http-header=Cookie={value}"); break;
                    default: media.AddOption($":{key}={value}"); break;
                }
            }
        }
        return media;
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

        StopAndDetachVideo();

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
            DisposePlayback();
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
        _wasPlayingBeforeAppSwitch = _currentMedia != null && _mediaPlayer?.State is VLCState.Playing or VLCState.Buffering;
        if (_wasPlayingBeforeAppSwitch)
            _mediaPlayer?.Pause();
        PrivacyOverlay.IsVisible = true;
    }

    private void OnApplicationResumed()
    {
        if (!_wasPlayingBeforeAppSwitch || _isClosing)
        {
            PrivacyOverlay.IsVisible = false;
            return;
        }
        _wasPlayingBeforeAppSwitch = false;
        _ = ResumePlaybackAfterAppSwitchAsync();
    }

    private async Task ResumePlaybackAfterAppSwitchAsync()
    {
        await Task.Delay(200);
        if (_isClosing || _mediaPlayer == null || _currentMedia == null) return;

        if (VideoPlayer.VideoView is { } videoView)
        {
            videoView.MediaPlayer = null;
            videoView.MediaPlayer = _mediaPlayer;
        }

        if (_mediaPlayer.State is VLCState.Stopped or VLCState.Ended or VLCState.Error)
            _mediaPlayer.Play(_currentMedia);
        else if (_mediaPlayer.State == VLCState.Paused)
            _mediaPlayer.Play();
        await Task.Delay(250);
        await MainThread.InvokeOnMainThreadAsync(() =>
        {
            RefreshVideoSurfaceSize();
            PrivacyOverlay.IsVisible = false;
        });
    }

    private void DisposePlayback()
    {
        var media = _currentMedia;
        var player = _mediaPlayer;
        var libVlc = _libVlc;
        _mediaPlayer = null;
        _libVlc = null;
        _currentMedia = null;

        try { player?.Stop(); }
        catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"Failed to stop media player: {ex.Message}"); }
        if (player != null)
            player.Playing -= OnMediaPlayerPlaying;
        try { media?.Dispose(); }
        catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"Failed to dispose media: {ex.Message}"); }
        try { player?.Dispose(); }
        catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"Failed to dispose media player: {ex.Message}"); }
        try { libVlc?.Dispose(); }
        catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"Failed to dispose LibVLC: {ex.Message}"); }
    }

    private void StopAndDetachVideo()
    {
        try { _mediaPlayer?.Stop(); }
        catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"Failed to stop media before leaving: {ex.Message}"); }

        try
        {
            if (VideoPlayer.VideoView != null)
                VideoPlayer.VideoView.MediaPlayer = null;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to detach video surface: {ex.Message}");
        }
    }
}
