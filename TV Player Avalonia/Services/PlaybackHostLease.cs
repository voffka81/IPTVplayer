namespace TV_Player.AvaloniaApp.Services;

/// <summary>Detaches the native host before releasing its adapter and player, once per attachment.</summary>
public sealed class PlaybackHostLease(Action detachHost, IDisposable adapter, IDisposable player) : IDisposable
{
    private bool _released;
    private bool _releasing;
    public bool IsReleased => _released;

    public void Dispose()
    {
        if (_released || _releasing) return;
        _releasing = true;
        try
        {
            // If detaching fails, keep the player alive and allow a later retry.
            detachHost();
            _released = true;
            try { adapter.Dispose(); }
            finally { player.Dispose(); }
        }
        finally { _releasing = false; }
    }
}
