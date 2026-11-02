namespace TV_Player.Playback;

/// <summary>
/// Cancelable, one-shot timeout used while waiting for a stream to start.
/// Each Arm replaces the previous timeout; callbacks are never invoked after cancellation.
/// </summary>
public sealed class PlaybackWatchdog : IDisposable
{
    private readonly TimeSpan _timeout;
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
        ObjectDisposedException.ThrowIf(_disposed, this);
        Cancel();
        var pending = new CancellationTokenSource();
        _pending = pending;
        _ = WaitAsync(pending.Token, onTimeout);
    }

    private async Task WaitAsync(CancellationToken token, Action<CancellationToken> onTimeout)
    {
        try
        {
            await Task.Delay(_timeout, token).ConfigureAwait(false);
            if (!token.IsCancellationRequested)
                onTimeout(token);
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested)
        {
        }
    }

    public void Cancel() => _pending?.Cancel();

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        Cancel();
        _pending?.Dispose();
        _pending = null;
    }
}
