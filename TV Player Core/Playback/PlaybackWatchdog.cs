namespace TV_Player.Playback;

/// <summary>Cancelable one-shot timeout; rearming invalidates the previous stream timeout.</summary>
public sealed class PlaybackWatchdog : IDisposable
{
    private readonly TimeSpan _timeout;
    private readonly object _gate = new();
    private CancellationTokenSource? _pending;
    private bool _disposed;

    public PlaybackWatchdog(TimeSpan timeout)
    {
        if (timeout <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(timeout));
        _timeout = timeout;
    }

    public void Arm(Action<CancellationToken> onTimeout)
    {
        ArgumentNullException.ThrowIfNull(onTimeout);
        CancellationTokenSource? previous;
        CancellationTokenSource current;
        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            previous = _pending;
            current = new CancellationTokenSource();
            _pending = current;
        }
        previous?.Cancel();
        _ = WaitAsync(current, onTimeout);
    }

    private async Task WaitAsync(CancellationTokenSource pending, Action<CancellationToken> onTimeout)
    {
        var token = pending.Token;
        try
        {
            await Task.Delay(_timeout, token).ConfigureAwait(false);
            if (!token.IsCancellationRequested)
                onTimeout(token);
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested)
        {
        }
        finally
        {
            lock (_gate)
            {
                if (ReferenceEquals(_pending, pending))
                    _pending = null;
            }
            pending.Dispose();
        }
    }

    public void Cancel()
    {
        CancellationTokenSource? pending;
        lock (_gate)
        {
            pending = _pending;
            _pending = null;
        }
        pending?.Cancel();
    }

    public void Dispose()
    {
        lock (_gate)
        {
            if (_disposed) return;
            _disposed = true;
        }
        Cancel();
    }
}
