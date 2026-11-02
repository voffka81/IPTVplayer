using TV_Player.Playback;
using Xunit;

namespace TV_Player.Tests;

/// <summary>Contract tests for engine consumers, without loading native VLC libraries.</summary>
public sealed class PlaybackEngineContractTests
{
    [Fact]
    public async Task Play_ExposesSourceAndPlayingState()
    {
        await using var engine = new FakeEngine();
        var changes = new List<PlaybackState>();
        engine.StateChanged += (_, e) => changes.Add(e.Current);
        await engine.PlayAsync(new StreamSource("https://example.test/live.m3u8"));
        Assert.Equal("https://example.test/live.m3u8", engine.Source?.Url);
        Assert.Equal(PlaybackState.Playing, engine.State);
        Assert.Equal(new[] { PlaybackState.Opening, PlaybackState.Playing }, changes);
    }

    [Fact]
    public async Task Cancellation_DoesNotChangeCurrentSource()
    {
        await using var engine = new FakeEngine();
        await engine.PlayAsync(new StreamSource("first"));
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => engine.PlayAsync(new StreamSource("second"), cts.Token));
        Assert.Equal("first", engine.Source?.Url);
    }

    [Fact]
    public async Task Stop_AndDispose_ReleaseSourceAndRejectFurtherPlayback()
    {
        var engine = new FakeEngine();
        await engine.PlayAsync(new StreamSource("first"));
        await engine.StopAsync();
        Assert.Null(engine.Source);
        Assert.Equal(PlaybackState.Stopped, engine.State);
        await engine.DisposeAsync();
        await engine.DisposeAsync();
        await Assert.ThrowsAsync<ObjectDisposedException>(() => engine.PlayAsync(new StreamSource("second")));
    }

    private sealed class FakeEngine : IPlaybackEngine
    {
        private bool _disposed;
        public string Id => "fake";
        public PlaybackState State { get; private set; } = PlaybackState.Stopped;
        public int Volume { get; set; } = 100;
        public StreamSource? Source { get; private set; }
        public event EventHandler<PlaybackStateChangedEventArgs>? StateChanged;
        public event EventHandler<PlaybackErrorEventArgs>? ErrorOccurred;

        public Task PlayAsync(StreamSource source, CancellationToken cancellationToken = default)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            cancellationToken.ThrowIfCancellationRequested();
            Source = source;
            Change(PlaybackState.Opening);
            Change(PlaybackState.Playing);
            return Task.CompletedTask;
        }
        public Task PauseAsync(CancellationToken cancellationToken = default)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            cancellationToken.ThrowIfCancellationRequested();
            Change(PlaybackState.Paused);
            return Task.CompletedTask;
        }
        public Task StopAsync(CancellationToken cancellationToken = default)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            cancellationToken.ThrowIfCancellationRequested();
            Source = null;
            Change(PlaybackState.Stopped);
            return Task.CompletedTask;
        }
        public ValueTask DisposeAsync()
        {
            _disposed = true;
            Source = null;
            Change(PlaybackState.Stopped);
            return ValueTask.CompletedTask;
        }
        private void Change(PlaybackState state)
        {
            var previous = State;
            State = state;
            StateChanged?.Invoke(this, new PlaybackStateChangedEventArgs(previous, state));
        }
    }
}
