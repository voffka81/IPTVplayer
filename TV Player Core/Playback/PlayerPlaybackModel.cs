namespace TV_Player.Playback;

public enum PlayerPlaybackPhase { Idle, Resolving, Connecting, Playing, Buffering, Paused, Recovering, Error, Stopped, Ended, Disposed }
public enum PlayerPlaybackFailure { None, NoStream, ResolutionFailed, PlaybackFailed, EngineUnavailable }
public enum PlayerPlaybackCommandKind { Connect, Opening, Playing, Buffering, Pause, Resume, Suspend, Stop, Recover, Fail, End, NativeStopped }
public readonly record struct PlaybackToken(long Generation, long Epoch);
public readonly record struct PlayerPlaybackCommand(PlayerPlaybackCommandKind Kind, long Generation,
    long? Epoch = null, string? Url = null, float Percent = 0,
    PlayerPlaybackFailure Failure = PlayerPlaybackFailure.None, Exception? Cause = null)
{
    public static PlayerPlaybackCommand FromBackend(PlaybackState state, PlaybackToken token) => new(state switch
    {
        PlaybackState.Opening => PlayerPlaybackCommandKind.Opening,
        PlaybackState.Playing => PlayerPlaybackCommandKind.Playing,
        PlaybackState.Buffering => PlayerPlaybackCommandKind.Buffering,
        PlaybackState.Paused => PlayerPlaybackCommandKind.Pause,
        PlaybackState.Stopped => PlayerPlaybackCommandKind.NativeStopped,
        PlaybackState.Failed => PlayerPlaybackCommandKind.Fail,
        _ => throw new ArgumentOutOfRangeException(nameof(state))
    }, token.Generation, token.Epoch, Failure: state == PlaybackState.Failed ? PlayerPlaybackFailure.PlaybackFailed : PlayerPlaybackFailure.None);
}
public readonly record struct PlayerPlaybackSnapshot(PlaybackToken Token, PlayerPlaybackPhase Phase, string? StreamUrl,
    bool IsLoading, bool HasStarted, bool IsSuspended, PlayerPlaybackFailure Failure, Exception? Cause)
{
    public bool CanAutoHideControls => !IsSuspended && HasStarted && !IsLoading &&
        Phase is PlayerPlaybackPhase.Playing or PlayerPlaybackPhase.Buffering;
}

/// <summary>UI-thread state/command contract shared by clients. Native resources and localized text stay outside Core.</summary>
public sealed class PlayerPlaybackModel : IDisposable
{
    private readonly PlaybackUiState _loading = new();
    private long _generation;
    private long _epoch;
    private string? _url;
    private bool _suspended;
    private bool _resolving;
    private PlayerPlaybackPhase _phase;
    private PlayerPlaybackFailure _failure;
    private Exception? _cause;
    public PlayerPlaybackSnapshot Snapshot { get; private set; }
    public event Action<PlayerPlaybackSnapshot>? Changed;

    public long Begin()
    {
        ObjectDisposedException.ThrowIf(_phase == PlayerPlaybackPhase.Disposed, this);
        ++_generation;
        ++_epoch;
        _url = null;
        _resolving = true;
        _failure = PlayerPlaybackFailure.None;
        _cause = null;
        _loading.Begin();
        _phase = _suspended ? PlayerPlaybackPhase.Stopped : PlayerPlaybackPhase.Resolving;
        Publish();
        return _generation;
    }

    public bool IsCurrent(PlaybackToken token) => _phase != PlayerPlaybackPhase.Disposed && token == Snapshot.Token;
    public bool CanStart(string url) => !_suspended && TransportActive && _url == url.Trim();
    private bool TransportActive => _url != null && _phase is PlayerPlaybackPhase.Connecting or PlayerPlaybackPhase.Playing
        or PlayerPlaybackPhase.Buffering or PlayerPlaybackPhase.Recovering;

    public bool Apply(PlayerPlaybackCommand command)
    {
        if (_phase == PlayerPlaybackPhase.Disposed || command.Generation != _generation ||
            (command.Epoch is { } epoch && epoch != _epoch)) return false;
        switch (command.Kind)
        {
            case PlayerPlaybackCommandKind.Connect:
                if (_generation == 0 || string.IsNullOrWhiteSpace(command.Url) ||
                    (!_resolving && _phase is not (PlayerPlaybackPhase.Connecting or PlayerPlaybackPhase.Recovering))) return false;
                _resolving = false;
                _url = command.Url.Trim();
                ++_epoch;
                _phase = _suspended ? PlayerPlaybackPhase.Stopped : PlayerPlaybackPhase.Connecting;
                _loading.Begin();
                break;
            case PlayerPlaybackCommandKind.Opening:
                if (_suspended || !TransportActive || _loading.HasStarted) return false;
                _phase = PlayerPlaybackPhase.Connecting;
                _loading.Begin();
                break;
            case PlayerPlaybackCommandKind.Buffering:
                if (_suspended || !TransportActive) return false;
                _loading.Buffering(command.Percent);
                _phase = _loading.HasStarted && command.Percent >= 100 ? PlayerPlaybackPhase.Playing : PlayerPlaybackPhase.Buffering;
                break;
            case PlayerPlaybackCommandKind.Playing:
                if (_suspended || !TransportActive) return false;
                _phase = PlayerPlaybackPhase.Playing;
                _loading.Playing();
                break;
            case PlayerPlaybackCommandKind.Pause:
                if (_suspended || !TransportActive) return false;
                _phase = PlayerPlaybackPhase.Paused;
                _loading.End();
                break;
            case PlayerPlaybackCommandKind.Suspend:
                if (_suspended) return false;
                _suspended = true;
                ++_epoch;
                _phase = PlayerPlaybackPhase.Stopped;
                _loading.End();
                break;
            case PlayerPlaybackCommandKind.Resume:
                if (!_suspended && _phase != PlayerPlaybackPhase.Paused) return false;
                _suspended = false;
                if (_url != null)
                {
                    _failure = PlayerPlaybackFailure.None;
                    _cause = null;
                    _phase = PlayerPlaybackPhase.Connecting;
                    _loading.Begin();
                }
                else
                {
                    _phase = _failure != PlayerPlaybackFailure.None ? PlayerPlaybackPhase.Error
                        : _resolving ? PlayerPlaybackPhase.Resolving
                        : _generation == 0 ? PlayerPlaybackPhase.Idle : PlayerPlaybackPhase.Stopped;
                    if (_phase == PlayerPlaybackPhase.Resolving) _loading.Begin(); else _loading.End();
                }
                break;
            case PlayerPlaybackCommandKind.Recover:
                if (_suspended || !TransportActive) return false;
                ++_epoch;
                _phase = PlayerPlaybackPhase.Recovering;
                _loading.Begin();
                break;
            case PlayerPlaybackCommandKind.Fail:
                if (command.Epoch != null && (_suspended || !TransportActive)) return false;
                _resolving = false;
                _failure = command.Failure == PlayerPlaybackFailure.None ? PlayerPlaybackFailure.PlaybackFailed : command.Failure;
                _cause = command.Cause;
                _phase = _suspended ? PlayerPlaybackPhase.Stopped : PlayerPlaybackPhase.Error;
                _loading.End();
                break;
            case PlayerPlaybackCommandKind.End:
                if (_suspended || !TransportActive) return false;
                _phase = PlayerPlaybackPhase.Ended;
                _loading.End();
                break;
            case PlayerPlaybackCommandKind.NativeStopped:
                if (_suspended || !TransportActive || !_loading.HasStarted) return false;
                _phase = PlayerPlaybackPhase.Stopped;
                _loading.End();
                break;
            case PlayerPlaybackCommandKind.Stop:
                ++_generation;
                ++_epoch;
                _url = null;
                _resolving = false;
                _suspended = false;
                _failure = PlayerPlaybackFailure.None;
                _cause = null;
                _phase = PlayerPlaybackPhase.Stopped;
                _loading.End();
                break;
            default: return false;
        }
        Publish();
        return true;
    }

    public void Dispose()
    {
        if (_phase == PlayerPlaybackPhase.Disposed) return;
        ++_epoch;
        _url = null;
        _resolving = false;
        _suspended = false;
        _failure = PlayerPlaybackFailure.None;
        _cause = null;
        _phase = PlayerPlaybackPhase.Disposed;
        _loading.End();
        Publish();
        Changed = null;
    }

    private void Publish()
    {
        var state = new PlayerPlaybackSnapshot(new(_generation, _epoch), _phase, _url,
            !_suspended && _loading.IsBuffering, _loading.HasStarted, _suspended, _failure, _cause);
        if (state == Snapshot) return;
        Snapshot = state;
        Changed?.Invoke(state);
    }
}
