namespace TV_Player.Playback;

/// <summary>
/// Platform-neutral playback lifecycle. Native player ownership stays in platform adapters.
/// A generation identifies a stream request so late callbacks can be rejected.
/// </summary>
public enum PlaybackSessionState { Idle, Resolving, Buffering, Playing, Recovering, Ended, Error, Disposed }

public sealed class PlaybackSession : IDisposable
{
    public PlaybackSessionState State { get; private set; } = PlaybackSessionState.Idle;
    public long Generation { get; private set; }
    public string? StreamUrl { get; private set; }
    public event Action<PlaybackSessionState>? StateChanged;

    public long Begin(string streamUrl)
    {
        ObjectDisposedException.ThrowIf(State == PlaybackSessionState.Disposed, this);
        ArgumentException.ThrowIfNullOrWhiteSpace(streamUrl);
        Generation++;
        StreamUrl = streamUrl.Trim();
        Transition(PlaybackSessionState.Resolving);
        return Generation;
    }

    public bool TryTransition(long generation, PlaybackSessionState next)
    {
        if (generation != Generation || State == PlaybackSessionState.Disposed)
            return false;
        if (next is PlaybackSessionState.Idle or PlaybackSessionState.Disposed or PlaybackSessionState.Resolving)
            return false;

        var allowed = State switch
        {
            PlaybackSessionState.Resolving => next is PlaybackSessionState.Buffering or PlaybackSessionState.Playing or PlaybackSessionState.Error or PlaybackSessionState.Recovering,
            PlaybackSessionState.Buffering => next is PlaybackSessionState.Playing or PlaybackSessionState.Recovering or PlaybackSessionState.Error or PlaybackSessionState.Ended,
            PlaybackSessionState.Playing => next is PlaybackSessionState.Buffering or PlaybackSessionState.Recovering or PlaybackSessionState.Error or PlaybackSessionState.Ended,
            PlaybackSessionState.Recovering => next is PlaybackSessionState.Buffering or PlaybackSessionState.Playing or PlaybackSessionState.Error,
            _ => false
        };
        if (!allowed) return false;
        Transition(next);
        return true;
    }

    public void Reset()
    {
        if (State == PlaybackSessionState.Disposed) return;
        Generation++;
        StreamUrl = null;
        Transition(PlaybackSessionState.Idle);
    }

    public void Dispose()
    {
        if (State == PlaybackSessionState.Disposed) return;
        Generation++;
        StreamUrl = null;
        Transition(PlaybackSessionState.Disposed);
        StateChanged = null;
    }

    private void Transition(PlaybackSessionState next)
    {
        if (State == next) return;
        State = next;
        StateChanged?.Invoke(next);
    }
}
