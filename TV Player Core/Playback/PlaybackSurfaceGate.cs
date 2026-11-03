namespace TV_Player.Playback;

/// <summary>Starts only the newest pending operation once a native video surface is usable.</summary>
public sealed class PlaybackSurfaceGate : IDisposable
{
    private bool _ready;
    private bool _disposed;
    private Action? _pending;

    public void Request(Action start)
    {
        if (_disposed) return;
        _pending = start;
        Flush();
    }

    public void SetReady(bool ready)
    {
        if (_disposed) return;
        _ready = ready;
        Flush();
    }

    public void Clear() => _pending = null;

    public void Dispose()
    {
        _disposed = true;
        _ready = false;
        Clear();
    }

    private void Flush()
    {
        if (!_ready || _pending == null) return;
        var start = _pending;
        _pending = null;
        start();
    }
}
