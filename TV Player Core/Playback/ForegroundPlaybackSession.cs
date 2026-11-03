namespace TV_Player.Playback;

/// <summary>UI-thread lifecycle policy: stop in background, restart live playback on a fresh surface.</summary>
public sealed class ForegroundPlaybackSession(Action<string> start, Action stop) : IDisposable
{
    private readonly PlaybackSurfaceGate _surface = new();
    private string? _streamUrl;
    private bool _foreground;
    private bool _disposed;

    public bool HasStream => _streamUrl != null;
    public bool IsForeground => _foreground && !_disposed;

    public void Play(string streamUrl)
    {
        if (_disposed) return;
        ArgumentException.ThrowIfNullOrWhiteSpace(streamUrl);
        _streamUrl = streamUrl;
        _surface.Request(() => start(streamUrl));
    }

    public void SetForeground(bool foreground)
    {
        if (_disposed || _foreground == foreground) return;
        _foreground = foreground;
        if (foreground) return; // Require fresh native-surface confirmation before restarting.
        _surface.SetReady(false);
        if (_streamUrl is { } url) _surface.Request(() => start(url));
        stop();
    }

    public void SetSurfaceReady(bool ready)
    {
        if (!_disposed) _surface.SetReady(_foreground && ready);
    }

    public void Stop()
    {
        if (_disposed) return;
        _foreground = false;
        _streamUrl = null;
        _surface.SetReady(false);
        _surface.Clear();
        stop();
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _streamUrl = null;
        _surface.Dispose();
        stop();
    }
}
