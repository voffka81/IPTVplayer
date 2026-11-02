using TV_Player.Playback;
using Xunit;

namespace TV_Player.Tests;

/// <summary>
/// Exercises the shared playback state, stale callback guard, and alternate URL queue
/// together without depending on native LibVLC or UI scheduling.
/// </summary>
public sealed class PlaybackRecoveryRegressionTests
{
    [Fact]
    public void RapidChannelSwitch_RejectsOldCallbacksAndKeepsNewChannelBuffering()
    {
        using var session = new PlaybackSession();
        var ui = new PlaybackUiState();
        var player = new object();

        var first = session.Begin("channel-one");
        session.TryTransition(first, PlaybackSessionState.Buffering);
        ui.Begin();

        var second = session.Begin("channel-two");
        session.TryTransition(second, PlaybackSessionState.Buffering);
        ui.Begin();

        if (PlaybackCallbackGuard.IsCurrent(first, session.Generation, player, player))
        {
            session.TryTransition(first, PlaybackSessionState.Playing);
            ui.Playing();
        }

        Assert.Equal(PlaybackSessionState.Buffering, session.State);
        Assert.True(ui.IsBuffering);
        Assert.False(ui.HasStarted);
        Assert.True(PlaybackCallbackGuard.IsCurrent(second, session.Generation, player, player));
    }

    [Fact]
    public void WatchdogRecovery_TriesEachAlternateOnlyOnce()
    {
        using var session = new PlaybackSession();
        var ui = new PlaybackUiState();
        var queue = new StreamFallbackQueue();
        var generation = session.Begin("primary");
        session.TryTransition(generation, PlaybackSessionState.Buffering);
        ui.Begin();
        queue.Reset(new[] { "backup-one", "backup-two" });

        Assert.True(queue.TryNext(out var first));
        Assert.Equal("backup-one", first);
        Assert.True(session.TryTransition(generation, PlaybackSessionState.Recovering));
        ui.Begin();

        Assert.True(queue.TryNext(out var second));
        Assert.Equal("backup-two", second);
        Assert.False(queue.TryNext(out _));
        Assert.Equal(2, queue.Attempted);

        Assert.True(session.TryTransition(generation, PlaybackSessionState.Playing));
        ui.Playing();
        Assert.False(ui.IsBuffering);
        Assert.True(ui.CanAutoHideOverlay);
    }

    [Fact]
    public void ExhaustedFallbacks_ErrorClearsBufferingAndCannotResumeTerminalSession()
    {
        using var session = new PlaybackSession();
        var ui = new PlaybackUiState();
        var queue = new StreamFallbackQueue();
        var generation = session.Begin("primary");
        session.TryTransition(generation, PlaybackSessionState.Buffering);
        ui.Begin();
        queue.Reset(Array.Empty<string>());

        Assert.False(queue.TryNext(out _));
        Assert.True(session.TryTransition(generation, PlaybackSessionState.Error));
        ui.End();

        Assert.Equal(PlaybackSessionState.Error, session.State);
        Assert.False(ui.IsBuffering);
        Assert.False(ui.HasStarted);
        Assert.False(ui.CanAutoHideOverlay);
        Assert.False(session.TryTransition(generation, PlaybackSessionState.Playing));
    }

    [Fact]
    public void SwitchingChannels_ResetsAlternatesAndInvalidatesPreviousGeneration()
    {
        using var session = new PlaybackSession();
        var queue = new StreamFallbackQueue();
        var oldGeneration = session.Begin("old");
        queue.Reset(new[] { "old-backup" });
        Assert.True(queue.TryNext(out _));

        var newGeneration = session.Begin("new");
        queue.Reset(new[] { "new-backup" });

        Assert.False(session.TryTransition(oldGeneration, PlaybackSessionState.Error));
        Assert.Equal(0, queue.Attempted);
        Assert.True(queue.TryNext(out var url));
        Assert.Equal("new-backup", url);
        Assert.False(queue.HasNext);
    }

    [Fact]
    public void BufferingAfterPlaybackStarted_DoesNotReopenLoadingIndicator()
    {
        var ui = new PlaybackUiState();
        ui.Begin();
        ui.Playing();
        ui.Buffering(20);
        Assert.True(ui.HasStarted);
        Assert.False(ui.IsBuffering);
        Assert.True(ui.CanAutoHideOverlay);
    }
}
