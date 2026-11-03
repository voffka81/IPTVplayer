using System.Globalization;
using TV_Player.Playback;
using TV_Player.Providers;

namespace TV_Player.Presentation;

public enum PlayerStatus { None, Resolving, Connecting, NoStream, ResolutionFailed, PlaybackFailed, EngineUnavailable, Ended }

public sealed record PlayerPresentationOptions(
    GuideWindowOrigin GuideOrigin = GuideWindowOrigin.CurrentOrNextOrFirst,
    bool IncludeNextProgram = true,
    int GuideWindowSize = 7);

public sealed record PlayerPresentationSnapshot(
    long ChannelVersion, M3UInfo? Channel, string ChannelTitle, IReadOnlyList<M3UInfo> Channels,
    PlayerPlaybackSnapshot Playback, PlayerStatus Status, ResolvedStream? Stream,
    ProgramGuideState Guide, EpgProgramItem? Program, bool IsNextProgram, bool IsGuideLoading, Exception? GuideError);

/// <summary>
/// Shared player presentation and commands. Public commands run on the UI thread;
/// asynchronous results return through the injected dispatcher. Owns requests/state,
/// not providers, clocks/timers, localized text, native players or UI controls.
/// </summary>
public sealed class PlayerPresentationModel : IDisposable
{
    private readonly StreamResolutionService _streams;
    private readonly Func<M3UInfo, Task<ProgramGuide?>> _loadGuide;
    private readonly Func<Action, Task> _dispatch;
    private readonly Func<DateTime> _now;
    private readonly IFormatProvider _format;
    private readonly PlayerPresentationOptions _options;
    private CancellationTokenSource? _guideRequest;
    private long _channelVersion;
    private long _guideVersion;
    private M3UInfo? _channel;
    private string _title = string.Empty;
    private ProgramGuide? _guide;
    private ProgramGuideState _guideState;
    private ResolvedStream? _stream;
    private bool _guideLoading;
    private Exception? _guideError;
    private bool _disposed;

    public PlayerPlaybackModel Playback { get; } = new();
    public ChannelSelectionState Selection { get; } = new();
    public PlayerPresentationSnapshot Snapshot { get; private set; }
    public event Action<PlayerPresentationSnapshot>? Changed;
    public event Action<ResolvedStream>? StreamRequested;

    public PlayerPresentationModel(StreamResolutionService streams, Func<M3UInfo, Task<ProgramGuide?>> loadGuide,
        Func<Action, Task> dispatch, Func<DateTime>? now = null, PlayerPresentationOptions? options = null,
        IFormatProvider? formatProvider = null)
    {
        _streams = streams;
        _loadGuide = loadGuide;
        _dispatch = dispatch;
        _now = now ?? (() => DateTime.Now);
        _options = options ?? new();
        _format = formatProvider ?? CultureInfo.InvariantCulture;
        _guideState = ProjectGuide(); // Validate options before subscribing.
        Snapshot = CreateSnapshot();
        Playback.Changed += OnPlaybackChanged;
        Selection.ChannelRequested += OnChannelRequested;
    }

    public void SetChannels(IEnumerable<M3UInfo> channels)
    {
        if (_disposed) return;
        Selection.SetChannels(channels);
        if (_channel != null) Selection.Sync(_channel);
        Publish();
    }

    public bool SelectChannel(M3UInfo channel) => !_disposed && Selection.Select(channel);
    public bool SwitchChannel(int direction) => !_disposed && Selection.Switch(direction);
    private void OnChannelRequested(M3UInfo channel) => _ = OpenAsync(channel);

    public Task OpenAsync(M3UInfo channel)
    {
        if (_disposed) return Task.CompletedTask;
        ArgumentNullException.ThrowIfNull(channel);
        var version = ++_channelVersion;
        _streams.Cancel();
        CancelGuide();
        _channel = channel;
        _title = channel.Name ?? string.Empty;
        Selection.Sync(channel);
        _stream = null;
        _guide = null;
        _guideState = ProjectGuide();
        _guideError = null;
        _guideLoading = true;
        var generation = Playback.Begin(); // Publishes the reset as one consistent snapshot.
        // Subscribers can issue another channel command while the reset is rendered.
        if (!IsCurrentChannel(version)) return Task.CompletedTask;
        var guideTask = ReloadGuideAsync();
        if (!IsCurrentChannel(version)) return guideTask;
        // Guide loading must never delay starting the video stream.
        return Task.WhenAll(ResolveStreamAsync(channel, version, generation), guideTask);
    }

    private async Task ResolveStreamAsync(M3UInfo channel, long version, long generation)
    {
        var result = await _streams.ResolveAsync(channel).ConfigureAwait(false);
        if (_disposed) return;
        await _dispatch(() =>
        {
            if (!IsCurrentChannel(version) || !_streams.IsCurrent(result) ||
                Playback.Snapshot.Token.Generation != generation) return;
            if (result.Status == StreamResolutionStatus.Resolved)
            {
                // Snapshot exposes the resolved source only after Connect is accepted.
                var previous = _stream;
                _stream = result.Stream;
                if (!Playback.Apply(new(PlayerPlaybackCommandKind.Connect, generation, Url: result.Stream!.Url)))
                {
                    _stream = previous;
                    return;
                }
                if (IsCurrentChannel(version) && ReferenceEquals(_stream, result.Stream))
                    StreamRequested?.Invoke(result.Stream);
            }
            else
                Playback.Apply(new(PlayerPlaybackCommandKind.Fail, generation,
                    Failure: result.Status == StreamResolutionStatus.NoStream ? PlayerPlaybackFailure.NoStream : PlayerPlaybackFailure.ResolutionFailed,
                    Cause: result.Error));
        }).ConfigureAwait(false);
    }

    // Readiness sources may replay while OpenAsync is already loading this guide.
    public Task NotifyGuideAvailableAsync() => _guideLoading ? Task.CompletedTask : ReloadGuideAsync();

    public async Task ReloadGuideAsync()
    {
        if (_disposed || _channel == null) return;
        CancelGuide();
        var version = _guideVersion;
        var channelVersion = _channelVersion;
        var channel = _channel;
        _guideRequest = new CancellationTokenSource();
        var token = _guideRequest.Token;
        _guideLoading = true;
        _guideError = null;
        Publish();
        if (token.IsCancellationRequested || !IsCurrentChannel(channelVersion) || version != _guideVersion) return;
        try
        {
            var pending = _loadGuide(channel);
            // Sources without cancellation can finish/fault after a channel change.
            _ = pending.ContinueWith(static task => { _ = task.Exception; }, CancellationToken.None,
                TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
            var guide = await pending.WaitAsync(token).ConfigureAwait(false);
            if (_disposed || token.IsCancellationRequested) return;
            await _dispatch(() =>
            {
                if (!IsCurrentChannel(channelVersion) || version != _guideVersion || token.IsCancellationRequested) return;
                _guide = guide;
                _guideLoading = false;
                RefreshGuide();
            }).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { }
        catch (Exception error)
        {
            if (_disposed || token.IsCancellationRequested) return;
            await _dispatch(() =>
            {
                if (!IsCurrentChannel(channelVersion) || version != _guideVersion || token.IsCancellationRequested) return;
                _guideLoading = false;
                _guideError = error;
                Publish(); // EPG failure must not turn healthy video into a playback error.
            }).ConfigureAwait(false);
        }
    }

    public void RefreshGuide()
    {
        if (_disposed) return;
        _guideState = ProjectGuide();
        Publish();
    }

    public void CancelPending()
    {
        if (_disposed) return;
        ++_channelVersion;
        _streams.Cancel();
        CancelGuide();
        _guideLoading = false;
        Publish();
    }

    private void CancelGuide()
    {
        ++_guideVersion;
        _guideRequest?.Cancel();
        _guideRequest?.Dispose();
        _guideRequest = null;
    }

    private bool IsCurrentChannel(long version) => !_disposed && version == _channelVersion;
    private ProgramGuideState ProjectGuide() => ProgramGuideProjection.Project(_guide, _now(),
        _options.GuideWindowSize, _options.GuideOrigin, _format);
    private void OnPlaybackChanged(PlayerPlaybackSnapshot _) => Publish();

    private PlayerPresentationSnapshot CreateSnapshot()
    {
        var playback = Playback.Snapshot;
        var program = _guideState.Current ?? (_options.IncludeNextProgram ? _guideState.Next : null);
        return new(_channelVersion, _channel, _title, Selection.Channels, playback, StatusFor(playback),
            _stream, _guideState, program, program != null && _guideState.Current == null, _guideLoading, _guideError);
    }

    public static PlayerStatus StatusFor(PlayerPlaybackSnapshot state) => state.IsSuspended ? PlayerStatus.None : state.Phase switch
    {
        PlayerPlaybackPhase.Resolving => PlayerStatus.Resolving,
        PlayerPlaybackPhase.Connecting or PlayerPlaybackPhase.Buffering or PlayerPlaybackPhase.Recovering
            when !state.HasStarted => PlayerStatus.Connecting,
        PlayerPlaybackPhase.Error => state.Failure switch
        {
            PlayerPlaybackFailure.NoStream => PlayerStatus.NoStream,
            PlayerPlaybackFailure.ResolutionFailed => PlayerStatus.ResolutionFailed,
            PlayerPlaybackFailure.EngineUnavailable => PlayerStatus.EngineUnavailable,
            _ => PlayerStatus.PlaybackFailed
        },
        PlayerPlaybackPhase.Ended => PlayerStatus.Ended,
        _ => PlayerStatus.None
    };

    private void Publish()
    {
        if (_disposed) return;
        var state = CreateSnapshot();
        if (state == Snapshot) return;
        Snapshot = state;
        Changed?.Invoke(state);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        CancelGuide();
        _streams.Dispose();
        Selection.ChannelRequested -= OnChannelRequested;
        Playback.Changed -= OnPlaybackChanged;
        Playback.Dispose();
        _guideLoading = false;
        _stream = null;
        Snapshot = CreateSnapshot();
        Changed = null;
        StreamRequested = null;
    }
}
