namespace TV_Player.Playback;

public enum PlaybackState
{
    Stopped,
    Opening,
    Playing,
    Paused,
    Buffering,
    Failed
}

public sealed record StreamSource(
    string Url,
    IReadOnlyDictionary<string, string>? Headers = null,
    IReadOnlyList<string>? FallbackUrls = null);

public sealed class PlaybackStateChangedEventArgs(PlaybackState previous, PlaybackState current) : EventArgs
{
    public PlaybackState Previous { get; } = previous;
    public PlaybackState Current { get; } = current;
}

public sealed class PlaybackErrorEventArgs(Exception exception) : EventArgs
{
    public Exception Exception { get; } = exception;
}

/// <summary>
/// Controls a playback backend independently of the UI toolkit and video surface.
/// Implementations own their native resources and must dispose them.
/// </summary>
public interface IPlaybackEngine : IAsyncDisposable
{
    string Id { get; }
    PlaybackState State { get; }
    int Volume { get; set; }

    event EventHandler<PlaybackStateChangedEventArgs>? StateChanged;
    event EventHandler<PlaybackErrorEventArgs>? ErrorOccurred;

    Task PlayAsync(StreamSource source, CancellationToken cancellationToken = default);
    Task PauseAsync(CancellationToken cancellationToken = default);
    Task StopAsync(CancellationToken cancellationToken = default);
}
