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
    private CancellationTokenSource? _guideLoadToken;
    private CancellationTokenSource? _playbackRequestToken;
    private System.Threading.Timer? _guideRefreshTimer;
    private bool _isClosing;
    private bool _wasPlayingBeforeAppSwitch;
    private M3UInfo? _selectedChannel;
    private readonly List<(M3UInfo Channel, Border Tile)> _channelPickerTiles = new();

    public PlayerPage(M3UInfo channel, IReadOnlyList<M3UInfo> channels, ProgramsData sourceData)
    {
        InitializeComponent();
        PlayerLayout.SizeChanged += (_, _) => RefreshVideoSurfaceSize();
        _sourceData = sourceData;
        _selectedChannel = channel;
        ChannelTitle.Text = channel.Name;
        ClockLabel.Text = DateTime.Now.ToString("HH:mm");
        foreach (var item in channels)
        {
            ChannelPickerItems.Children.Add(CreateChannelPickerTile(item));
        }
        PlayChannel(channel);
    }

    private View CreateChannelPickerTile(M3UInfo channel)
    {
        var logoContainer = new Grid { HeightRequest = 44, WidthRequest = 76 };

        if (Uri.TryCreate(channel.Logo, UriKind.Absolute, out var logoUri) &&
            (logoUri.Scheme == Uri.UriSchemeHttp || logoUri.Scheme == Uri.UriSchemeHttps))
        {
            logoContainer.Children.Add(new Image
            {
                Source = new UriImageSource
                {
                    Uri = logoUri,
                    CachingEnabled = true,
                    CacheValidity = TimeSpan.FromDays(7)
                },
                Aspect = Aspect.AspectFit,
                InputTransparent = true
            });
        }

        var nameLabel = new Label
        {
            Text = channel.Name,
            FontSize = 12,
            LineBreakMode = LineBreakMode.TailTruncation,
            MaxLines = 2,
            HorizontalTextAlignment = TextAlignment.Center,
            VerticalTextAlignment = TextAlignment.Center
        };
        var content = new VerticalStackLayout
        {
            Spacing = 4,
            HorizontalOptions = LayoutOptions.Fill,
            VerticalOptions = LayoutOptions.Fill,
            InputTransparent = true,
            Children = { logoContainer, nameLabel }
        };
        var tile = new Border
        {
            WidthRequest = 112,
            HeightRequest = 88,
            Padding = new Thickness(8, 5),
            BackgroundColor = Color.FromArgb("#162130"),
            Stroke = Color.FromArgb("#304054"),
            StrokeThickness = 1,
            StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 10 },
            Content = new Grid { Children = { content } }
        };
        _channelPickerTiles.Add((channel, tile));
        UpdateChannelPickerSelection();
        var button = new Button
        {
            BackgroundColor = Colors.Transparent,
            BorderColor = Colors.Transparent,
            BorderWidth = 0,
            CornerRadius = 10,
            Padding = 0,
            Text = string.Empty
        };
        button.Clicked += (_, _) =>
        {
            ChannelPicker.IsVisible = false;
            PlayChannel(channel);
        };
        button.Focused += (_, _) =>
        {
            tile.Stroke = Color.FromArgb("#7BD0FF");
            tile.StrokeThickness = 2;
        };
        button.Unfocused += (_, _) => UpdateChannelPickerSelection();
        ((Grid)tile.Content).Children.Add(button);
        return tile;
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
        UpdateChannelPickerSelection();

        _playbackRequestToken?.Cancel();
        _playbackRequestToken?.Dispose();
        _playbackRequestToken = new CancellationTokenSource();
        var playbackToken = _playbackRequestToken.Token;

        _ = LoadProgramGuideAsync(channel);
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
        _guideLoadToken?.Cancel();
        _playbackRequestToken?.Cancel();
        _guideRefreshTimer?.Dispose();

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
            await ScrollSelectedChannelIntoViewAsync();
    }

    private async Task ScrollSelectedChannelIntoViewAsync()
    {
        for (var attempt = 0; attempt < 8 && ChannelPicker.ContentSize.Width <= ChannelPicker.Width; attempt++)
            await Task.Delay(50);
        var selectedTile = _channelPickerTiles
            .Where(item => IsSameChannel(item.Channel, _selectedChannel))
            .Select(item => item.Tile)
            .FirstOrDefault();
        if (selectedTile == null || ChannelPicker.Width <= 0) return;

        var maxOffset = Math.Max(0, ChannelPicker.ContentSize.Width - ChannelPicker.Width);
        var offset = Math.Clamp(selectedTile.X + selectedTile.Width / 2 - ChannelPicker.Width / 2, 0, maxOffset);
        await ChannelPicker.ScrollToAsync(offset, 0, animated: false);
    }

    private void OnGuideInfoTapped(object? sender, TappedEventArgs e)
    {
        ProgramGuideList.IsVisible = !ProgramGuideList.IsVisible;
        ChannelPicker.IsVisible = false;
        ShowPlayerUi();
    }

    private async Task LoadProgramGuideAsync(M3UInfo channel)
    {
        _guideLoadToken?.Cancel();
        _guideLoadToken?.Dispose();
        _guideLoadToken = new CancellationTokenSource();
        var token = _guideLoadToken.Token;
        _guideRefreshTimer?.Dispose();
        _guideRefreshTimer = null;
        await MainThread.InvokeOnMainThreadAsync(() =>
        {
            ProgramInfoPanel.IsVisible = false;
            ProgramGuideList.IsVisible = false;
            ProgramGuideItems.Children.Clear();
        });

        try
        {
            var guide = await _sourceData.GetGuideByProgram(channel);
            if (token.IsCancellationRequested) return;

            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                if (token.IsCancellationRequested) return;
                if (guide?.Programs == null || guide.Programs.Count == 0)
                {
                    ProgramInfoPanel.IsVisible = false;
                    return;
                }

                var orderedPrograms = guide.Programs.OrderBy(item => item.StartTime).ToList();
                var firstVisibleProgram = orderedPrograms.FindIndex(item => item.StartTime <= DateTime.Now && item.EndTime >= DateTime.Now);
                if (firstVisibleProgram < 0)
                    firstVisibleProgram = orderedPrograms.FindIndex(item => item.StartTime > DateTime.Now);
                if (firstVisibleProgram < 0) firstVisibleProgram = 0;

                foreach (var item in orderedPrograms.Skip(firstVisibleProgram).Take(7))
                    AddGuideItem(item);

                UpdateProgramInfo(guide);
                _guideRefreshTimer = new System.Threading.Timer(
                    _ => MainThread.BeginInvokeOnMainThread(() => UpdateProgramInfo(guide)),
                    null,
                    TimeSpan.FromMinutes(1),
                    TimeSpan.FromMinutes(1));
            });
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to load program guide: {ex.Message}");
        }
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

    private void AddGuideItem(ProgramInfo item)
    {
        var timeLabel = new Label
        {
            Text = $"{item.StartTime:HH:mm} – {item.EndTime:HH:mm}",
            FontSize = 12,
            TextColor = Color.FromArgb("#8DD2FF")
        };
        var titleLabel = new Label
        {
            Text = item.Title,
            FontSize = 13,
            LineBreakMode = LineBreakMode.TailTruncation,
            WidthRequest = 160
        };
        var card = new Border
        {
            BackgroundColor = Color.FromArgb("#162130"),
            Stroke = Color.FromArgb("#304054"),
            StrokeThickness = 1,
            StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 10 },
            Padding = new Thickness(10, 6),
            Content = new VerticalStackLayout { Spacing = 2, Children = { timeLabel, titleLabel } }
        };
        ProgramGuideItems.Children.Add(card);
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
        _guideLoadToken?.Cancel();
        _guideRefreshTimer?.Dispose();
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

    private void UpdateChannelPickerSelection()
    {
        foreach (var (channel, tile) in _channelPickerTiles)
        {
            var isSelected = IsSameChannel(channel, _selectedChannel);
            tile.BackgroundColor = Color.FromArgb(isSelected ? "#1A4263" : "#162130");
            tile.Stroke = Color.FromArgb(isSelected ? "#55B9FF" : "#304054");
            tile.StrokeThickness = isSelected ? 2 : 1;
        }
    }

    private static bool IsSameChannel(M3UInfo channel, M3UInfo? selected)
    {
        if (selected == null) return false;
        if (ReferenceEquals(channel, selected)) return true;
        if (!string.IsNullOrWhiteSpace(channel.ChannelId) && !string.IsNullOrWhiteSpace(selected.ChannelId))
            return string.Equals(channel.ProviderId, selected.ProviderId, StringComparison.OrdinalIgnoreCase) &&
                   string.Equals(channel.ChannelId, selected.ChannelId, StringComparison.OrdinalIgnoreCase);
        return string.Equals(channel.Url, selected.Url, StringComparison.OrdinalIgnoreCase) &&
               string.Equals(channel.Name, selected.Name, StringComparison.OrdinalIgnoreCase);
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
