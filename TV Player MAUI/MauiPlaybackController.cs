using LibVLCSharp.Shared;
using TV_Player.Playback;

namespace TV_Player.Maui;

/// <summary>Owns LibVLC resources and stream media. The page only binds the video surface.</summary>
internal sealed class MauiPlaybackController : IDisposable
{
    private readonly PlaybackSurfaceGate _surfaceGate = new();
    private int _playGeneration;
    private int _resumeGeneration;
    private bool _disposed;
    private LibVLC? _libVlc;
    private MediaPlayer? _mediaPlayer;
    private Media? _currentMedia;
    private bool _wasPlayingBeforeAppSwitch;
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
    }

    public bool HasMedia => _currentMedia != null && _mediaPlayer != null;

    public void SetSurfaceReady(bool ready) => _surfaceGate.SetReady(ready);

    public void Play(string streamUrl)
    {
        if (_disposed) return;
        ++_playGeneration;
        _wasPlayingBeforeAppSwitch = false;
        _surfaceGate.Request(() => PlayOnSurface(streamUrl));
    }

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

    public void PauseForAppSwitch()
    {
        _surfaceGate.SetReady(false);
        _resumeGeneration = _playGeneration;
        _wasPlayingBeforeAppSwitch = _currentMedia != null && _mediaPlayer?.State is VLCState.Playing or VLCState.Buffering;
        if (_wasPlayingBeforeAppSwitch) _mediaPlayer?.Pause();
    }

    public bool ShouldResume => _wasPlayingBeforeAppSwitch;

    public async Task ResumeAfterAppSwitchAsync(CancellationToken token)
    {
        var generation = _resumeGeneration;
        _wasPlayingBeforeAppSwitch = false;
        await Task.Delay(200, token);
        if (token.IsCancellationRequested || _disposed || generation != _playGeneration) return;
        _surfaceGate.Request(() =>
        {
            if (token.IsCancellationRequested || _disposed || generation != _playGeneration ||
                _mediaPlayer == null || _currentMedia == null) return;
            _detach(_mediaPlayer);
            if (_mediaPlayer.State is VLCState.Stopped or VLCState.Ended or VLCState.Error)
                _mediaPlayer.Play(_currentMedia);
            else if (_mediaPlayer.State == VLCState.Paused)
                _mediaPlayer.Play();
        });
    }

    public void StopAndDetach()
    {
        ++_playGeneration;
        _surfaceGate.Clear();
        try { _mediaPlayer?.Stop(); }
        catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"Failed to stop media before leaving: {ex.Message}"); }
        _detach(null);
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

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _surfaceGate.Dispose();
        var media = _currentMedia;
        var player = _mediaPlayer;
        var libVlc = _libVlc;
        _mediaPlayer = null;
        _libVlc = null;
        _currentMedia = null;

        try { player?.Stop(); }
        catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"Failed to stop media player: {ex.Message}"); }
        if (player != null)
        {
            player.Playing -= OnPlaying;
            player.EncounteredError -= OnError;
        }
        try { media?.Dispose(); }
        catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"Failed to dispose media: {ex.Message}"); }
        try { player?.Dispose(); }
        catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"Failed to dispose media player: {ex.Message}"); }
        try { libVlc?.Dispose(); }
        catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"Failed to dispose LibVLC: {ex.Message}"); }
    }


}
