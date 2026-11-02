using LibVLCSharp.Shared;
using TV_Player.Maui.Services;

namespace TV_Player.Maui;

public partial class PlayerPage : ContentPage
{
    private LibVLC? _libVlc;
    private MediaPlayer? _mediaPlayer;
    private Media? _currentMedia;

    public PlayerPage(M3UInfo channel, IReadOnlyList<M3UInfo> channels)
    {
        InitializeComponent();
        ChannelTitle.Text = channel.Name;
        ClockLabel.Text = DateTime.Now.ToString("HH:mm");
        foreach (var item in channels)
        {
            var button = new Button { Text = item.Name, WidthRequest = 112, HeightRequest = 76, FontSize = 12, LineBreakMode = LineBreakMode.TailTruncation };
            button.Clicked += (_, _) => PlayChannel(item);
            ChannelPickerItems.Children.Add(button);
        }
        PlayChannel(channel);
    }

    private void PlayChannel(M3UInfo channel)
    {
        if (string.IsNullOrWhiteSpace(channel.Url))
        {
            StatusLabel.Text = "Для этого источника Android-плеер пока не может разрешить адрес канала.";
            return;
        }
        ChannelTitle.Text = channel.Name;
        StatusLabel.Text = "Подключение…";
        _libVlc ??= new LibVLC(enableDebugLogs: false);
        _mediaPlayer ??= new MediaPlayer(_libVlc);
        VideoPlayer.MediaPlayer = _mediaPlayer;
        _currentMedia?.Dispose();
        _currentMedia = CreateMedia(channel.Url);
        _mediaPlayer.Play(_currentMedia);
        StatusLabel.Text = string.Empty;
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

    private async void OnBackClicked(object sender, EventArgs e) => await Navigation.PopAsync();
    private void OnPickerClicked(object sender, EventArgs e) => ChannelPicker.IsVisible = !ChannelPicker.IsVisible;

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        _mediaPlayer?.Stop();
        _currentMedia?.Dispose();
        _mediaPlayer?.Dispose();
        _libVlc?.Dispose();
        _mediaPlayer = null;
        _libVlc = null;
        _currentMedia = null;
    }
}
