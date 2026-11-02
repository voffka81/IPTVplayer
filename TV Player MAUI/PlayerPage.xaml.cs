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
        if (Platform.CurrentActivity is MainActivity activity)
            activity.SetImmersiveMode(true);
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

        if (Navigation.NavigationStack.Count > 1)
            await Navigation.PopAsync();

        DisposePlayback();
    }

    private void OnVideoTapped(object? sender, TappedEventArgs e)
    {
        if (PlayerTopBar.IsVisible || PlayerBottomBar.IsVisible)
        {
            _hideUiToken?.Cancel();
            ChannelPicker.IsVisible = false;
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
        ShowPlayerUi();
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
                if (!token.IsCancellationRequested && !ChannelPicker.IsVisible)
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
#if ANDROID
        if (Platform.CurrentActivity is MainActivity activity)
            activity.SetImmersiveMode(false);
#endif
    }

    private void DisposePlayback()
    {
        _mediaPlayer?.Stop();
        _currentMedia?.Dispose();
        _mediaPlayer?.Dispose();
        _libVlc?.Dispose();
        _mediaPlayer = null;
        _libVlc = null;
        _currentMedia = null;
    }
}
