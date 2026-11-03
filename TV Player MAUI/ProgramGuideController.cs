using TV_Player;

namespace TV_Player.Maui;

/// <summary>
/// Coordinates guide requests and periodic refresh without owning player UI elements.
/// All callbacks run on the MAUI main thread; stale requests cannot publish results.
/// </summary>
internal sealed class ProgramGuideController : IDisposable
{
    private readonly ProgramsData _source;
    private readonly Action _onReset;
    private readonly Action<ProgramGuide> _onLoaded;
    private readonly Action<ProgramGuide> _onRefresh;
    private CancellationTokenSource? _request;
    private System.Threading.Timer? _timer;
    private int _version;
    private bool _disposed;

    public ProgramGuideController(
        ProgramsData source,
        Action onReset,
        Action<ProgramGuide> onLoaded,
        Action<ProgramGuide> onRefresh)
    {
        _source = source;
        _onReset = onReset;
        _onLoaded = onLoaded;
        _onRefresh = onRefresh;
    }

    public async Task LoadAsync(M3UInfo channel)
    {
        if (_disposed) return;
        var version = ++_version;
        _request?.Cancel();
        _request?.Dispose();
        _request = new CancellationTokenSource();
        var token = _request.Token;
        _timer?.Dispose();
        _timer = null;

        await MainThread.InvokeOnMainThreadAsync(() =>
        {
            if (IsCurrent(version, token)) _onReset();
        });

        try
        {
            var guide = await _source.GetGuideByProgram(channel);
            if (!IsCurrent(version, token)) return;

            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                if (!IsCurrent(version, token)) return;
                if (guide?.Programs == null || guide.Programs.Count == 0) return;
                _onLoaded(guide);
                _timer = new System.Threading.Timer(
                    _ => MainThread.BeginInvokeOnMainThread(() =>
                    {
                        if (IsCurrent(version, token)) _onRefresh(guide);
                    }),
                    null, TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(1));
            });
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to load program guide: {ex.Message}");
        }
    }

    private bool IsCurrent(int version, CancellationToken token) =>
        !_disposed && !token.IsCancellationRequested && version == _version;

    public void Cancel()
    {
        ++_version;
        _request?.Cancel();
        _timer?.Dispose();
        _timer = null;
    }

    public void Dispose()
    {
        Cancel();
        _disposed = true;
        _request?.Dispose();
        _request = null;
    }
}
