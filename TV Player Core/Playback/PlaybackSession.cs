namespace TV_Player.Playback;

/// <summary>
/// Platform-neutral playback lifecycle. Native player ownership stays in platform adapters.
/// A generation identifies a stream request so late callbacks can be rejected.
/// </summary>
public enum PlaybackState { Idle, Resolving, Buffering, Playing, Recovering, Ended, Error, Disposed }

public sealed class PlaybackSession : IDisposable
{
    public PlaybackState State { get; private set; } = PlaybackState.Idle;
    public long Generation { get; private set; }
    public string? StreamUrl { get; private set; }
    public event Action<PlaybackState>? StateChanged;

    public long Begin(string streamUrl)
    {
        ObjectDisposedException.ThrowIf(State == PlaybackState.Disposed, this);
        ArgumentException.ThrowIfNullOrWhiteSpace(streamUrl);
        Generation++;
        StreamUrl = streamUrl.Trim();
        Transition(PlaybackState.Resolving);
        return Generation;
    }

    public bool TryTransition(long generation, PlaybackState next)
    {
        if (generation != Generation || State == PlaybackState.Disposed)
            return false;
        if (next is PlaybackState.Idle or PlaybackState.Disposed or PlaybackState.Resolving)
            return false;

        var allowed = State switch
        {
            PlaybackState.Resolving => next is PlaybackState.Buffering or PlaybackState.Playing or PlaybackState.Error or PlaybackState.Recovering,
            PlaybackState.Buffering => next is PlaybackState.Playing or PlaybackState.Recovering or PlaybackState.Error or PlaybackState.Ended,
            PlaybackState.Playing => next is PlaybackState.Buffering or PlaybackState.Recovering or PlaybackState.Error or PlaybackState.Ended,
            PlaybackState.Recovering => next is PlaybackState.Buffering or PlaybackState.Playing or PlaybackState.Error,
            _ => false
        };
        if (!allowed) return false;
        Transition(next);
        return true;
    }

    public void Reset()
    {
        if (State == PlaybackState.Disposed) return;
        Generation++;
        StreamUrl = null;
        Transition(PlaybackState.Idle);
    }

    public void Dispose()
    {
        if (State == PlaybackState.Disposed) return;
        Generation++;
        StreamUrl = null;
        Transition(PlaybackState.Disposed);
        StateChanged = null;
    }

    private void Transition(PlaybackState next)
    {
        if (State == next) return;
        State = next;
        StateChanged?.Invoke(next);
    }
}
