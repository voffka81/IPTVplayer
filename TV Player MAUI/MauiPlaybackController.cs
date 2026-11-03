using LibVLCSharp.Shared;
using TV_Player.Playback;

namespace TV_Player.Maui;

/// <summary>Owns LibVLC resources and stream media. The page only binds the video surface.</summary>
internal sealed class MauiPlaybackController : IDisposable
{
    private readonly ForegroundPlaybackSession _session;
    private bool _disposed;
    private LibVLC? _libVlc;
    private MediaPlayer? _mediaPlayer;
    private Media? _currentMedia;
    private readonly Action<MediaPlayer> _bind;
    private readonly Action<MediaPlayer?> _detach;
    private readonly Action _onPlaying;
    private readonly Action _onError;

    public MauiPlaybackController(Action<MediaPlayer> bind, Action<MediaPlayer?> detach, Action onPlaying, Action onError)
    {
        _bind = bind;
        _detach = detach;
        _onPlaying = onPlaying;
        _onError = onError;
        _session = new ForegroundPlaybackSession(PlayOnSurface, ReleasePlayer);
    }

    public bool HasStream => _session.HasStream;
    public bool IsForeground => _session.IsForeground;

    public void SetForeground(bool foreground) => _session.SetForeground(foreground);
    public void SetSurfaceReady(bool ready) => _session.SetSurfaceReady(ready);
    public void Play(string streamUrl) => _session.Play(streamUrl);
    public void StopAndDetach() => _session.Stop();

    private void PlayOnSurface(string streamUrl)
    {
        _libVlc ??= new LibVLC(enableDebugLogs: false);
        _mediaPlayer ??= new MediaPlayer(_libVlc);
        _mediaPlayer.Playing -= OnPlaying;
        _mediaPlayer.Playing += OnPlaying;
        _mediaPlayer.EncounteredError -= OnError;
        _mediaPlayer.EncounteredError += OnError;
        _bind(_mediaPlayer);
        _currentMedia?.Dispose();
        _currentMedia = CreateMedia(streamUrl);
        if (!_mediaPlayer.Play(_currentMedia))
            throw new InvalidOperationException("VLC не смог начать воспроизведение потока.");
    }

    private void OnPlaying(object? sender, EventArgs e) => _onPlaying();
    private void OnError(object? sender, EventArgs e) => _onError();

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

    private void ReleasePlayer()
    {
        var media = _currentMedia;
        var player = _mediaPlayer;
        _mediaPlayer = null;
        _currentMedia = null;
        if (player != null)
        {
            player.Playing -= OnPlaying;
            player.EncounteredError -= OnError;
        }
        // Live streams may not support Pause. Stop closes demux/network activity and a
        // new MediaPlayer is created on foreground entry, with a new Android vout.
        try { player?.Stop(); }
        catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"Failed to stop media player: {ex.Message}"); }
        try { _detach(null); }
        catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"Failed to detach media player: {ex.Message}"); }
        try { media?.Dispose(); }
        catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"Failed to dispose media: {ex.Message}"); }
        try { player?.Dispose(); }
        catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"Failed to dispose media player: {ex.Message}"); }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _session.Dispose();
        var libVlc = _libVlc;
        _libVlc = null;
        try { libVlc?.Dispose(); }
        catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"Failed to dispose LibVLC: {ex.Message}"); }
    }
}
