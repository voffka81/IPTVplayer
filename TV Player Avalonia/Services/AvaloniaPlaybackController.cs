using Avalonia.Threading;
using LibVLCSharp.Shared;
using TV_Player.AvaloniaApp.ViewModels;
using TV_Player.Playback;

namespace TV_Player.AvaloniaApp.Services;

/// <summary>
/// Desktop native playback adapter. Owns media/engine, native subscriptions,
/// watchdog and reconciliation timer; the view owns the video host and MediaPlayer.
/// All public calls and dispatched native callbacks run on the UI thread.
/// </summary>
internal sealed class AvaloniaPlaybackController : IDisposable
{
    private readonly MediaPlayer _mediaPlayer;
    private readonly LibVlcPlaybackEngine _playbackEngine;
    private readonly DispatcherTimer _playbackUiTimer = new() { Interval = TimeSpan.FromMilliseconds(250) };
    private readonly PlaybackWatchdog _bufferingWatchdog = new(TimeSpan.FromSeconds(12));
    private readonly StreamFallbackQueue _fallbackStreams = new();
    private readonly Action _showOverlay;
    private readonly Action _onReconciled;
    private readonly Action<string> _log;
    private PlayerViewModel? _viewModel;
    private string? _activeStreamUrl;
    private bool _fallbackAttempted;
    private bool _disposed;
    private Action? _unsubscribePlayback;
    private bool _hasPlaybackStarted => _viewModel?.Playback.Snapshot.HasStarted == true;

    public AvaloniaPlaybackController(LibVLC libVlc, MediaPlayer player, PlayerViewModel? viewModel,
        Action showOverlay, Action onReconciled, Action<string> log)
    {
        _mediaPlayer = player;
        _playbackEngine = new LibVlcPlaybackEngine(libVlc, player);
        _viewModel = viewModel;
        _showOverlay = showOverlay;
        _onReconciled = onReconciled;
        _log = log;
        _playbackUiTimer.Tick += OnUiTick;
    }

    public void SetViewModel(PlayerViewModel? viewModel)
    {
        if (_disposed) return;
        _viewModel = viewModel;
        PlayCurrentStream();
    }

    private void OnUiTick(object? sender, EventArgs args) => ReconcilePlayingUi();
    private void Log(string message) => _log(message);

    // Invalidate callbacks before the view detaches its native video host.
    public void Suspend()
    {
        if (_disposed) return;
        ApplyPlaybackState(PlayerPlaybackCommandKind.Suspend);
        _unsubscribePlayback?.Invoke();
        _unsubscribePlayback = null;
    }

    // Called after VideoView.MediaPlayer is cleared, before MediaPlayer.Dispose.
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _playbackUiTimer.Stop();
        _playbackUiTimer.Tick -= OnUiTick;
        _bufferingWatchdog.Dispose();
        _unsubscribePlayback?.Invoke();
        _unsubscribePlayback = null;
        _playbackEngine.DisposeAsync().AsTask().GetAwaiter().GetResult();
        _viewModel = null;
    }

    // Native callbacks and watchdogs run on the UI thread after token validation.
    private void ApplyPlaybackState(PlayerPlaybackCommandKind command)
    {
        _bufferingWatchdog.Cancel();
        _playbackUiTimer.Stop();
        ReportPlayback(command);
    }

    private bool ReportPlayback(PlayerPlaybackCommandKind command, float percent = 0)
    {
        if (_viewModel == null) return false;
        var model = _viewModel.Playback;
        var token = model.Snapshot.Token;
        return model.Apply(new(command, token.Generation, token.Epoch, Percent: percent));
    }

    public void ReconcilePlayingUi()
    {
        if (_disposed || _viewModel == null || !_viewModel.Playback.CanStart(_activeStreamUrl ?? string.Empty) ||
            !_mediaPlayer.IsPlaying || _hasPlaybackStarted)
            return;

        ApplyPlaybackState(PlayerPlaybackCommandKind.Playing);
        _onReconciled();
    }

    public void PlayCurrentStream()
    {
        if (_disposed || _viewModel == null)
        {
            Log("PlayCurrentStream skipped: adapter disposed or no ViewModel");
            return;
        }

        if (string.IsNullOrWhiteSpace(_viewModel.StreamUrl))
        {
            _bufferingWatchdog.Cancel();
            _playbackUiTimer.Stop();
            _unsubscribePlayback?.Invoke();
            _unsubscribePlayback = null;
            _mediaPlayer.Stop();
            Log("PlayCurrentStream aborted: empty StreamUrl");
            return;
        }

        try
        {
            var streamUrl = _viewModel.StreamUrl.Trim();
            _viewModel.Playback.Apply(new(PlayerPlaybackCommandKind.Resume, _viewModel.Playback.Snapshot.Token.Generation));
            var token = _viewModel.Playback.Snapshot.Token;
            if (!_viewModel.Playback.Apply(new(PlayerPlaybackCommandKind.Connect, token.Generation, Url: streamUrl))) return;
            Log($"PlayCurrentStream start. Url={FormatUrlForLog(streamUrl)}");
            _playbackUiTimer.Start();
            _showOverlay();
            _activeStreamUrl = streamUrl;
            _fallbackStreams.Reset(_viewModel.FallbackStreamUrls);
            _fallbackAttempted = false;

            ReportPlayback(PlayerPlaybackCommandKind.Buffering);
            StartBufferingWatchdog();

            var started = StartPlayback(streamUrl, fallback: false);
            Log($"Primary playback start result: {started}");
            if (!started && !TryStartNextStreamFallback())
            {
                ApplyPlaybackState(PlayerPlaybackCommandKind.Fail);
                _viewModel!.SetPlaybackStatus("Stream failed to start. Try another channel or Open Externally.");
            }
        }
        catch (System.Exception ex)
        {
            Log($"PlayCurrentStream exception: {ex}");
            ApplyPlaybackState(PlayerPlaybackCommandKind.Fail);
            _viewModel!.SetPlaybackStatus($"Embedded playback failed: {ex.Message}. Use Open Externally as a fallback.");
        }
    }

    private bool StartPlayback(string streamUrl, bool fallback)
    {
        Log($"StartPlayback called. fallback={fallback}, url={FormatUrlForLog(streamUrl)}");
        try
        {
            BindPlaybackCallbacks();
            var source = new StreamSource(streamUrl);
            if (fallback)
                _playbackEngine.PlayFallbackAsync(source).GetAwaiter().GetResult();
            else
                _playbackEngine.PlayAsync(source).GetAwaiter().GetResult();
            return true;
        }
        catch (Exception ex)
        {
            Log($"StartPlayback failed: {ex}");
            return false;
        }
    }

    private void BindPlaybackCallbacks()
    {
        _unsubscribePlayback?.Invoke();
        var player = _mediaPlayer;
        var model = _viewModel!.Playback;
        var token = model.Snapshot.Token;
        void Dispatch(Action action) => DispatchPlaybackCallback(model, token, player, action);
        EventHandler<MediaPlayerBufferingEventArgs> buffering = (sender, args) => Dispatch(() => OnMediaPlayerBuffering(sender, args));
        EventHandler<EventArgs> playing = (sender, args) => Dispatch(() => OnMediaPlayerPlaying(sender, args));
        EventHandler<EventArgs> error = (sender, args) => Dispatch(() => OnMediaPlayerEncounteredError(sender, args));
        EventHandler<EventArgs> ended = (sender, args) => Dispatch(() => OnMediaPlayerEndReached(sender, args));
        EventHandler<EventArgs> paused = (_, _) => Dispatch(() => ApplyPlaybackState(PlayerPlaybackCommandKind.Pause));
        player.Buffering += buffering;
        player.Playing += playing;
        player.EncounteredError += error;
        player.EndReached += ended;
        player.Paused += paused;
        _unsubscribePlayback = () =>
        {
            player.Buffering -= buffering;
            player.Playing -= playing;
            player.EncounteredError -= error;
            player.EndReached -= ended;
            player.Paused -= paused;
        };
    }

    private void StartBufferingWatchdog()
    {
        var model = _viewModel?.Playback;
        var attempt = model?.Snapshot.Token;
        var player = _mediaPlayer;
        Log("Buffering watchdog started (12s)");

        _bufferingWatchdog.Arm(token =>
        {
            Dispatcher.UIThread.Post(() =>
            {
                if (_disposed || token.IsCancellationRequested ||
                    model == null || !ReferenceEquals(model, _viewModel?.Playback) ||
                    attempt == null || !model.IsCurrent(attempt.Value) || !ReferenceEquals(player, _mediaPlayer) ||
                    _viewModel == null || string.IsNullOrWhiteSpace(_activeStreamUrl))
                    return;

                ReconcilePlayingUi();
                if (_mediaPlayer.IsPlaying)
                    return;

                if (TryStartNextStreamFallback())
                    return;

                if (_fallbackAttempted)
                    return;

                _fallbackAttempted = true;
                Log("Buffering watchdog triggered fallback attempt");
                ReportPlayback(PlayerPlaybackCommandKind.Recover);
                var started = StartPlayback(_activeStreamUrl!, fallback: true);
                Log($"Fallback playback start result: {started}");
                if (!started)
                {
                    ApplyPlaybackState(PlayerPlaybackCommandKind.Fail);
                    _viewModel!.SetPlaybackStatus("Fallback playback failed. Try Open Externally for this channel.");
                }
            });
        });
    }

    private bool TryStartNextStreamFallback()
    {
        if (_viewModel == null || !_fallbackStreams.TryNext(out var fallbackUrl))
            return false;
        _activeStreamUrl = fallbackUrl;
        ReportPlayback(PlayerPlaybackCommandKind.Recover);
        _viewModel.Playback.Apply(new(PlayerPlaybackCommandKind.Connect, _viewModel.Playback.Snapshot.Token.Generation, Url: fallbackUrl));
        _playbackUiTimer.Start();
        _showOverlay();
        _fallbackAttempted = false;
        Log($"Trying alternate stream URL {_fallbackStreams.Attempted}/{_fallbackStreams.Total}: {FormatUrlForLog(fallbackUrl)}");

        _viewModel!.SetPlaybackStatus("Trying alternate stream…");
        StartBufferingWatchdog();

        var started = StartPlayback(fallbackUrl, fallback: false);
        Log($"Alternate stream start result: {started}");
        return started;
    }

    // The binding captures the attempt before native playback starts, rather than
    // reading the current channel when a late native event happens to arrive.
    private void DispatchPlaybackCallback(PlayerPlaybackModel model, PlaybackToken attempt, object player, Action action)
    {
        Dispatcher.UIThread.Post(() =>
        {
            if (_disposed || !ReferenceEquals(model, _viewModel?.Playback) || !model.IsCurrent(attempt) ||
                !ReferenceEquals(player, _mediaPlayer) || !model.CanStart(_activeStreamUrl ?? string.Empty)) return;
            action();
        });
    }

    private void OnMediaPlayerBuffering(object? sender, MediaPlayerBufferingEventArgs e)
    {
        if (_viewModel == null)
            return;

        if (e.Cache < 1 || ((int)e.Cache % 10) == 0)
        {
            Log($"MediaPlayer buffering: {e.Cache:0}%");
        }

        ReconcilePlayingUi();
        if (_viewModel != null)
        {
            ReportPlayback(PlayerPlaybackCommandKind.Buffering, e.Cache);
        }
    }

    private void OnMediaPlayerPlaying(object? sender, System.EventArgs e)
    {
        Log($"MediaPlayer event: Playing. audioTracks={_mediaPlayer?.AudioTrackCount}, audioTrack={_mediaPlayer?.AudioTrack}, volume={_mediaPlayer?.Volume}, muted={_mediaPlayer?.Mute}");
        ApplyPlaybackState(PlayerPlaybackCommandKind.Playing);
        _showOverlay();
    }

    private void OnMediaPlayerEncounteredError(object? sender, System.EventArgs e)
    {
        Log("MediaPlayer event: EncounteredError");
        if (TryStartNextStreamFallback())
            return;

        ApplyPlaybackState(PlayerPlaybackCommandKind.Fail);
        _showOverlay();
    }

    private void OnMediaPlayerEndReached(object? sender, System.EventArgs e)
    {
        Log("MediaPlayer event: EndReached");
        ApplyPlaybackState(PlayerPlaybackCommandKind.End);
        _showOverlay();
    }

    private static string FormatUrlForLog(string url)
    {
        if (string.IsNullOrWhiteSpace(url))
            return "<empty>";

        return url.Trim();
    }

}
