using TV_Player;

namespace TV_Player.Maui;

/// <summary>Resolves provider streams while discarding responses for superseded channels.</summary>
internal sealed class StreamResolutionController : IDisposable
{
    private readonly ProgramsData _source;
    private CancellationTokenSource? _request;
    private int _version;
    private bool _disposed;

    public StreamResolutionController(ProgramsData source) => _source = source;

    public async Task<StreamResolutionResult?> ResolveAsync(M3UInfo channel, Action? onResolving = null)
    {
        if (_disposed) return null;
        var version = ++_version;
        _request?.Cancel();
        _request?.Dispose();
        _request = new CancellationTokenSource();
        var token = _request.Token;
        var streamUrl = channel.Url;

        if (string.IsNullOrWhiteSpace(streamUrl) &&
            channel.ProviderId == _source.Provider.Id &&
            !string.IsNullOrWhiteSpace(channel.ChannelId))
        {
            onResolving?.Invoke();
            try
            {
                var resolved = await _source.Provider.ResolveStreamAsync(channel.ChannelId, token);
                if (!IsCurrent(version, token)) return null;
                streamUrl = resolved.PrimaryUrl;
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested) { return null; }
            catch (Exception ex)
            {
                if (!IsCurrent(version, token)) return null;
                return new StreamResolutionResult(null, $"Не удалось получить поток: {ex.Message}");
            }
        }

        if (!IsCurrent(version, token)) return null;
        return string.IsNullOrWhiteSpace(streamUrl)
            ? new StreamResolutionResult(null, "Для этого канала нет ссылки на поток.")
            : new StreamResolutionResult(streamUrl, null);
    }

    private bool IsCurrent(int version, CancellationToken token) =>
        !_disposed && !token.IsCancellationRequested && version == _version;

    public void Cancel()
    {
        ++_version;
        _request?.Cancel();
    }

    public void Dispose()
    {
        if (_disposed) return;
        Cancel();
        _disposed = true;
        _request?.Dispose();
        _request = null;
    }
}

internal sealed record StreamResolutionResult(string? Url, string? Error);
