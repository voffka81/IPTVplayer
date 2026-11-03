using TV_Player.Plugins;

namespace TV_Player.Providers;

public enum ProviderResolutionMode { WhenUrlMissing, PreferProvider }
public enum StreamResolutionStatus { Resolved, NoStream, ProviderFailed, Cancelled, Superseded, Disposed }
public sealed record ResolvedStream(string Url, IReadOnlyList<string> FallbackUrls, DateTimeOffset? ExpiresAt);
public sealed record StreamResolutionResult(long RequestId, StreamResolutionStatus Status,
    ResolvedStream? Stream = null, Exception? Error = null);

/// <summary>Latest-request-wins provider lookup. Neither provider ownership nor UI dispatch belongs here.</summary>
public sealed class StreamResolutionService : IDisposable
{
    private readonly object _sync = new();
    private readonly string? _providerId;
    private readonly Func<string, CancellationToken, Task<StreamInfo>>? _resolve;
    private readonly ProviderResolutionMode _mode;
    private sealed class Request(long id, CancellationTokenSource source)
    {
        public long Id { get; } = id;
        public CancellationTokenSource Source { get; } = source;
        public StreamResolutionStatus? Suppression { get; set; }
    }
    private Request? _activeRequest;
    private CancellationToken _externalToken;
    private long _generation;
    private bool _disposed;

    public StreamResolutionService(IChannelProvider? provider, ProviderResolutionMode mode = ProviderResolutionMode.WhenUrlMissing)
        : this(provider?.Id, provider == null ? null : provider.ResolveStreamAsync, mode) { }

    public StreamResolutionService(string? providerId, Func<string, CancellationToken, Task<StreamInfo>>? resolve,
        ProviderResolutionMode mode = ProviderResolutionMode.WhenUrlMissing)
    {
        if (!Enum.IsDefined(mode)) throw new ArgumentOutOfRangeException(nameof(mode));
        _providerId = providerId;
        _resolve = resolve;
        _mode = mode;
    }

    public bool RequiresProviderLookup(M3UInfo channel) => _resolve != null && !string.IsNullOrWhiteSpace(_providerId) &&
        string.Equals(channel.ProviderId, _providerId, StringComparison.Ordinal) &&
        !string.IsNullOrWhiteSpace(channel.ChannelId) &&
        (_mode == ProviderResolutionMode.PreferProvider || string.IsNullOrWhiteSpace(channel.Url));

    public async Task<StreamResolutionResult> ResolveAsync(M3UInfo channel, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(channel);
        Request request;
        Request? previous;
        // Snapshot mutable channel data before awaiting a provider.
        var url = channel.Url;
        var channelId = channel.ChannelId;
        var lookup = RequiresProviderLookup(channel);
        lock (_sync)
        {
            if (_disposed) return new(_generation, StreamResolutionStatus.Disposed);
            request = new Request(++_generation, CancellationTokenSource.CreateLinkedTokenSource(cancellationToken));
            previous = _activeRequest;
            if (previous != null) previous.Suppression = StreamResolutionStatus.Superseded;
            _activeRequest = request;
            _externalToken = cancellationToken;
        }
        // Provider cancellation callbacks may re-enter this service; never invoke them under the lock.
        CancelSource(previous?.Source);
        var token = request.Source.Token;
        try
        {
            token.ThrowIfCancellationRequested();
            StreamInfo stream;
            if (lookup)
            {
                var providerTask = _resolve!(channelId!, token);
                // A non-cooperative provider may fault after WaitAsync was cancelled.
                _ = providerTask.ContinueWith(static completed => { _ = completed.Exception; },
                    CancellationToken.None, TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously,
                    TaskScheduler.Default);
                stream = await providerTask.WaitAsync(token).ConfigureAwait(false);
            }
            else stream = new StreamInfo(url ?? string.Empty);
            token.ThrowIfCancellationRequested();
            return string.IsNullOrWhiteSpace(stream.PrimaryUrl)
                ? Complete(request, token, StreamResolutionStatus.NoStream)
                : Complete(request, token, StreamResolutionStatus.Resolved,
                    new(stream.PrimaryUrl, Array.AsReadOnly(stream.FallbackUrls?.ToArray() ?? Array.Empty<string>()), stream.ExpiresAt));
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested)
        {
            return Complete(request, token, StreamResolutionStatus.Cancelled);
        }
        catch (Exception ex)
        {
            return Complete(request, token, StreamResolutionStatus.ProviderFailed, error: ex);
        }
        finally
        {
            lock (_sync)
                if (ReferenceEquals(_activeRequest, request)) _activeRequest = null;
            request.Source.Dispose();
        }
    }

    // Clients must recheck after a UI dispatch: a previously valid result can be superseded while queued.
    public bool IsCurrent(StreamResolutionResult result)
    {
        lock (_sync)
            return !_disposed && result.RequestId == _generation && !_externalToken.IsCancellationRequested &&
                result.Status is StreamResolutionStatus.Resolved or StreamResolutionStatus.NoStream or StreamResolutionStatus.ProviderFailed;
    }

    private StreamResolutionResult Complete(Request request, CancellationToken token, StreamResolutionStatus status,
        ResolvedStream? stream = null, Exception? error = null)
    {
        lock (_sync)
        {
            if (_disposed) return new(request.Id, StreamResolutionStatus.Disposed);
            if (request.Suppression is { } suppression) return new(request.Id, suppression);
            if (request.Id != _generation) return new(request.Id, StreamResolutionStatus.Superseded);
            if (token.IsCancellationRequested || _externalToken.IsCancellationRequested)
                return new(request.Id, StreamResolutionStatus.Cancelled);
            return new(request.Id, status, stream, error);
        }
    }

    public void Cancel() => Invalidate(dispose: false);
    public void Dispose() => Invalidate(dispose: true);

    private void Invalidate(bool dispose)
    {
        CancellationTokenSource? source;
        lock (_sync)
        {
            if (_disposed) return;
            _disposed = dispose;
            ++_generation;
            var request = _activeRequest;
            if (request != null) request.Suppression = dispose ? StreamResolutionStatus.Disposed : StreamResolutionStatus.Cancelled;
            source = request?.Source;
            _activeRequest = null;
        }
        CancelSource(source);
    }

    private static void CancelSource(CancellationTokenSource? source)
    {
        try { source?.Cancel(); }
        catch (ObjectDisposedException) { } // The provider completed concurrently and released its source.
        catch (AggregateException) { System.Diagnostics.Debug.WriteLine("A provider cancellation callback failed."); }
    }
}
