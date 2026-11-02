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
    private System.Threading.Timer? _guideRefreshTimer;
    private bool _isClosing;

    public PlayerPage(M3UInfo channel, IReadOnlyList<M3UInfo> channels, ProgramsData sourceData)
    {
        InitializeComponent();
        _sourceData = sourceData;
        ChannelTitle.Text = channel.Name;
        ClockLabel.Text = DateTime.Now.ToString("HH:mm");
        foreach (var item in channels)
        {
            var button = new Button { Text = item.Name, WidthRequest = 112, HeightRequest = 76, FontSize = 12, LineBreakMode = LineBreakMode.TailTruncation };
            button.Clicked += (_, _) =>
            {
                ChannelPicker.IsVisible = false;
                PlayChannel(item);
            };
            ChannelPickerItems.Children.Add(button);
        }
        PlayChannel(channel);
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
#if ANDROID
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

    private void OnPlayerLoaded(object? sender, EventArgs e) => HideLibVlcControls();

    private void HideLibVlcControls()
    {
        if (VideoPlayer.PlaybackControls != null)
            VideoPlayer.PlaybackControls.IsVisible = false;
    }

    private async void PlayChannel(M3UInfo channel)
    {
        _ = LoadProgramGuideAsync(channel);
        var streamUrl = channel.Url;
        if (string.IsNullOrWhiteSpace(streamUrl) &&
            channel.ProviderId == _sourceData.Provider.Id &&
            !string.IsNullOrWhiteSpace(channel.ChannelId))
        {
            StatusLabel.Text = "Получение потока…";
            try
            {
                var resolved = await _sourceData.Provider.ResolveStreamAsync(channel.ChannelId);
                streamUrl = resolved.PrimaryUrl;
            }
            catch (Exception ex)
            {
                StatusLabel.Text = $"Не удалось получить поток: {ex.Message}";
                return;
            }
        }
        if (string.IsNullOrWhiteSpace(streamUrl))
        {
            StatusLabel.Text = "Для этого канала нет ссылки на поток.";
            return;
        }
        ChannelTitle.Text = channel.Name;
        StatusLabel.Text = "Подключение…";
        _libVlc ??= new LibVLC(enableDebugLogs: false);
        _mediaPlayer ??= new MediaPlayer(_libVlc);
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
        _guideRefreshTimer?.Dispose();

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

    private void OnPickerClicked(object sender, EventArgs e)
    {
        ChannelPicker.IsVisible = !ChannelPicker.IsVisible;
        ProgramGuideList.IsVisible = false;
        ShowPlayerUi();
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
        try { media?.Dispose(); }
        catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"Failed to dispose media: {ex.Message}"); }
        try { player?.Dispose(); }
        catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"Failed to dispose media player: {ex.Message}"); }
        try { libVlc?.Dispose(); }
        catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"Failed to dispose LibVLC: {ex.Message}"); }
    }
}
