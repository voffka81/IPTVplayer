using System.Web;
using LibVLCSharp.Shared;
using TV_Player.Playback;

namespace TV_Player.AvaloniaApp.Services;

/// <summary>LibVLC backend. Video surface and MediaPlayer lifetime remain with the host view.</summary>
public sealed class LibVlcPlaybackEngine : IPlaybackEngine
{
    private readonly LibVLC _libVlc;
    private readonly MediaPlayer _player;
    private Media? _media;
    private bool _disposed;
    private PlaybackState _state = PlaybackState.Stopped;

    public LibVlcPlaybackEngine(LibVLC libVlc, MediaPlayer player)
    {
        _libVlc = libVlc ?? throw new ArgumentNullException(nameof(libVlc));
        _player = player ?? throw new ArgumentNullException(nameof(player));
    }

    public string Id => "libvlc";
    public PlaybackState State => _state;
    public int Volume
    {
        get => _player.Volume;
        set { ObjectDisposedException.ThrowIf(_disposed, this); _player.Volume = Math.Clamp(value, 0, 100); }
    }

    public event EventHandler<PlaybackStateChangedEventArgs>? StateChanged;
    public event EventHandler<PlaybackErrorEventArgs>? ErrorOccurred;

    public Task PlayAsync(StreamSource source, CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(source);
        ArgumentException.ThrowIfNullOrWhiteSpace(source.Url);
        cancellationToken.ThrowIfCancellationRequested();
        StopNative();
        try
        {
            SetState(PlaybackState.Opening);
            _media = BuildMedia(_libVlc, source.Url, source.Headers);
            if (!_player.Play(_media))
                throw new InvalidOperationException("LibVLC failed to start playback.");
            SetState(PlaybackState.Playing);
            return Task.CompletedTask;
        }
        catch (Exception ex)
        {
            SetState(PlaybackState.Failed);
            ErrorOccurred?.Invoke(this, new PlaybackErrorEventArgs(ex));
            throw;
        }
    }

    public Task PauseAsync(CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        cancellationToken.ThrowIfCancellationRequested();
        _player.Pause();
        SetState(PlaybackState.Paused);
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        cancellationToken.ThrowIfCancellationRequested();
        StopNative();
        SetState(PlaybackState.Stopped);
        return Task.CompletedTask;
    }

    public ValueTask DisposeAsync()
    {
        if (_disposed) return ValueTask.CompletedTask;
        StopNative();
        _disposed = true;
        SetState(PlaybackState.Stopped);
        StateChanged = null;
        ErrorOccurred = null;
        return ValueTask.CompletedTask;
    }

    private void StopNative()
    {
        _player.Stop();
        _media?.Dispose();
        _media = null;
    }

    private void SetState(PlaybackState next)
    {
        if (_state == next) return;
        var previous = _state;
        _state = next;
        StateChanged?.Invoke(this, new PlaybackStateChangedEventArgs(previous, next));
    }

    private static Media BuildMedia(LibVLC libVlc, string rawUrl, IReadOnlyDictionary<string, string>? headers)
    {
        var splitIndex = rawUrl.IndexOf('|');
        var url = splitIndex > 0 ? rawUrl[..splitIndex].Trim() : rawUrl;
        var media = new Media(libVlc, url, FromType.FromLocation);
        media.AddOption(":network-caching=1500");
        media.AddOption(":live-caching=1500");
        media.AddOption(":http-reconnect=true");
        media.AddOption(":codec=avcodec");
        if (OperatingSystem.IsMacOS()) media.AddOption(":avcodec-hw=none");

        if (splitIndex > 0)
        {
            foreach (var pair in rawUrl[(splitIndex + 1)..].Split('&', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                var i = pair.IndexOf('=');
                if (i <= 0 || i == pair.Length - 1) continue;
                AddHeader(media, pair[..i], HttpUtility.UrlDecode(pair[(i + 1)..]));
            }
        }
        if (headers != null)
            foreach (var pair in headers)
                AddHeader(media, pair.Key, pair.Value);
        return media;
    }

    private static void AddHeader(Media media, string name, string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return;
        switch (name.Trim().ToLowerInvariant())
        {
            case "user-agent":
            case "http-user-agent": media.AddOption($":http-user-agent={value}"); break;
            case "referer":
            case "referrer":
            case "http-referrer":
            case "http-referer": media.AddOption($":http-referrer={value}"); break;
            case "origin": media.AddOption($":http-header=Origin={value}"); break;
            case "cookie":
            case "cookies": media.AddOption($":http-header=Cookie={value}"); break;
        }
    }
}
