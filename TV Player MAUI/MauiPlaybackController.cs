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
    private readonly Action _detach;
    private readonly PlayerPlaybackModel _model;
    private readonly Action<Action> _dispatch;
    private Action? _unsubscribe;

    public MauiPlaybackController(Action<MediaPlayer> bind, Action detach, PlayerPlaybackModel model, Action<Action> dispatch)
    {
        _bind = bind;
        _detach = detach;
        _model = model;
        _dispatch = dispatch;
        _session = new ForegroundPlaybackSession(PlayOnSurface, ReleasePlayer);
    }

    public void SetForeground(bool foreground)
    {
        _model.Apply(new(foreground ? PlayerPlaybackCommandKind.Resume : PlayerPlaybackCommandKind.Suspend,
            _model.Snapshot.Token.Generation));
        _session.SetForeground(foreground);
    }
    public void SetSurfaceReady(bool ready) => _session.SetSurfaceReady(ready);
    public void Play(string streamUrl) => _session.Play(streamUrl);
    public void StopAndDetach()
    {
        _model.Apply(new(PlayerPlaybackCommandKind.Stop, _model.Snapshot.Token.Generation));
        _session.Stop();
    }

    private void PlayOnSurface(string streamUrl)
    {
        if (!_model.CanStart(streamUrl)) return;
        ReleasePlayer();
        var token = _model.Snapshot.Token;
        try
        {
            _libVlc ??= new LibVLC(enableDebugLogs: false);
            var player = _mediaPlayer = new MediaPlayer(_libVlc);
            void Report(PlayerPlaybackCommandKind kind, float percent = 0) => _dispatch(() =>
            {
                if (!_disposed && ReferenceEquals(player, _mediaPlayer))
                    _model.Apply(new(kind, token.Generation, token.Epoch, Percent: percent));
            });
            EventHandler<EventArgs> playing = (_, _) => Report(PlayerPlaybackCommandKind.Playing);
            EventHandler<EventArgs> error = (_, _) => Report(PlayerPlaybackCommandKind.Fail);
            EventHandler<EventArgs> opening = (_, _) => Report(PlayerPlaybackCommandKind.Opening);
            EventHandler<EventArgs> paused = (_, _) => Report(PlayerPlaybackCommandKind.Pause);
            EventHandler<EventArgs> ended = (_, _) => Report(PlayerPlaybackCommandKind.End);
            EventHandler<MediaPlayerBufferingEventArgs> buffering = (_, e) => Report(PlayerPlaybackCommandKind.Buffering, e.Cache);
            player.Playing += playing;
            player.EncounteredError += error;
            player.EndReached += ended;
            player.Opening += opening;
            player.Paused += paused;
            player.Buffering += buffering;
            _unsubscribe = () =>
            {
                player.Playing -= playing;
                player.EncounteredError -= error;
                player.EndReached -= ended;
                player.Opening -= opening;
                player.Paused -= paused;
                player.Buffering -= buffering;
            };
            _bind(player);
            _currentMedia = CreateMedia(streamUrl);
            if (!player.Play(_currentMedia))
                throw new InvalidOperationException(TV_Player.Localization.I18n.Get("T1381e4353d98"));
        }
        catch (Exception ex)
        {
            _model.Apply(new(PlayerPlaybackCommandKind.Fail, token.Generation, token.Epoch, Cause: ex));
            ReleasePlayer();
        }
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

    private void ReleasePlayer()
    {
        var media = _currentMedia;
        var player = _mediaPlayer;
        _mediaPlayer = null;
        _currentMedia = null;
        _unsubscribe?.Invoke();
        _unsubscribe = null;
        // Live streams may not support Pause. Stop closes demux/network activity and a
        // new MediaPlayer is created on foreground entry, with a new Android vout.
        try { player?.Stop(); }
        catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"Failed to stop media player: {ex.Message}"); }
        try { _detach(); }
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
