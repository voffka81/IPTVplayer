using TV_Player.Playback;
using Xunit;

namespace TV_Player.Tests;

public class ForegroundPlaybackSessionTests
{
    private sealed class Player
    {
        public readonly List<string> Events = new();
        public int Instances { get; private set; }
        public bool Streaming { get; private set; }
        public void Start(string url) { Instances++; Streaming = true; Events.Add("play:" + url); }
        public void Stop() { Streaming = false; Events.Add("stop"); }
    }

    [Fact]
    public void BackgroundNeverStartsEvenWhenSurfaceCallbacksAndResolutionArrive()
    {
        var player = new Player();
        using var session = new ForegroundPlaybackSession(player.Start, player.Stop);
        session.Play("live-A");
        session.SetSurfaceReady(true);
        Assert.Empty(player.Events);
        Assert.False(player.Streaming);
    }

    [Fact]
    public void GoingIntoBackgroundStopsActualPlaybackRatherThanPausingIt()
    {
        var player = new Player();
        using var session = new ForegroundPlaybackSession(player.Start, player.Stop);
        session.SetForeground(true);
        session.SetSurfaceReady(true);
        session.Play("live-A");
        Assert.True(player.Streaming);
        session.SetForeground(false);
        Assert.False(player.Streaming);
        Assert.Equal(new[] { "play:live-A", "stop" }, player.Events);
    }

    [Fact]
    public void ForegroundRestartsAtLiveEdgeWithFreshPlayerOnlyAfterNewSurfaceConfirmation()
    {
        var player = new Player();
        using var session = new ForegroundPlaybackSession(player.Start, player.Stop);
        session.SetForeground(true);
        session.SetSurfaceReady(true);
        session.Play("live-A");
        session.SetForeground(false);
        session.SetSurfaceReady(true); // A stale Android callback while still backgrounded.
        session.SetForeground(true);
        Assert.Equal(1, player.Instances);
        Assert.False(player.Streaming);
        session.SetSurfaceReady(true);
        Assert.Equal(2, player.Instances);
        Assert.True(player.Streaming);
        Assert.Equal(new[] { "play:live-A", "stop", "play:live-A" }, player.Events);
    }

    [Fact]
    public void UserLeavePauseAndStopNotificationsAreIdempotent()
    {
        var player = new Player();
        using var session = new ForegroundPlaybackSession(player.Start, player.Stop);
        session.SetForeground(true);
        session.SetSurfaceReady(true);
        session.Play("live-A");
        session.SetForeground(false);
        session.SetForeground(false);
        session.SetForeground(false);
        Assert.Equal(1, player.Events.Count(value => value == "stop"));
        session.SetForeground(true);
        session.SetForeground(true);
        session.SetSurfaceReady(true);
        session.SetSurfaceReady(true);
        Assert.Equal(2, player.Instances);
    }

    [Fact]
    public void ProviderCompletionInBackgroundDoesNotStartStreamAndLatestChannelWins()
    {
        var player = new Player();
        using var session = new ForegroundPlaybackSession(player.Start, player.Stop);
        session.SetForeground(true);
        session.Play("old"); // URL received before the surface is usable.
        session.SetForeground(false);
        session.Play("new"); // New provider result arrives while inactive.
        session.SetSurfaceReady(true);
        Assert.Equal(0, player.Instances);
        session.SetForeground(true);
        session.SetSurfaceReady(true);
        Assert.Equal(new[] { "stop", "play:new" }, player.Events);
    }

    [Fact]
    public void RapidForegroundBackgroundBeforeSurfaceReadyCannotRunStaleResume()
    {
        var player = new Player();
        using var session = new ForegroundPlaybackSession(player.Start, player.Stop);
        session.SetForeground(true);
        session.SetSurfaceReady(true);
        session.Play("live-A");
        session.SetForeground(false);
        session.SetForeground(true);
        session.SetForeground(false);
        session.SetSurfaceReady(true);
        Assert.False(player.Streaming);
        Assert.Equal(1, player.Instances);
        session.SetForeground(true);
        session.SetSurfaceReady(true);
        Assert.Equal(2, player.Instances);
    }

    [Fact]
    public void PageDisappearingBeforeActivityPauseStillStopsAndRestartsOnReturn()
    {
        var player = new Player();
        using var session = new ForegroundPlaybackSession(player.Start, player.Stop);
        session.SetForeground(true);
        session.SetSurfaceReady(true);
        session.Play("live-A");
        session.SetForeground(false); // Page disappearance, before Activity's pause event.
        Assert.False(player.Streaming);
        session.SetForeground(false); // Activity pause after page unsubscribes.
        session.SetForeground(true);
        session.SetSurfaceReady(true);
        Assert.Equal(new[] { "play:live-A", "stop", "play:live-A" }, player.Events);
    }

    [Fact]
    public void LeavingPlayerPermanentlyClearsRestartIntent()
    {
        var player = new Player();
        using var session = new ForegroundPlaybackSession(player.Start, player.Stop);
        session.SetForeground(true);
        session.Play("live-A");
        session.SetForeground(false);
        session.Stop();
        session.SetForeground(true);
        session.SetSurfaceReady(true);
        Assert.False(session.HasStream);
        Assert.Equal(0, player.Instances);
    }

    [Fact]
    public void SuspendDuringConnectingRestartsWithoutNeedingPlayingEvent()
    {
        var player = new Player();
        using var session = new ForegroundPlaybackSession(player.Start, player.Stop);
        session.SetForeground(true);
        session.Play("connecting");
        session.SetForeground(false);
        session.SetForeground(true);
        session.SetSurfaceReady(true);
        Assert.Equal(new[] { "stop", "play:connecting" }, player.Events);
    }

    [Fact]
    public void RepeatedTaskSwitchesAlternateStopAndFreshStartWithoutDuplicatedPlayback()
    {
        var player = new Player();
        using var session = new ForegroundPlaybackSession(player.Start, player.Stop);
        session.Play("live-A");
        session.SetForeground(true);
        session.SetSurfaceReady(true);
        for (var cycle = 0; cycle < 50; cycle++)
        {
            session.SetForeground(false);
            session.SetForeground(false);
            Assert.False(player.Streaming);
            session.SetForeground(true);
            Assert.False(player.Streaming);
            session.SetSurfaceReady(true);
            session.SetSurfaceReady(true);
            Assert.True(player.Streaming);
        }
        Assert.Equal(51, player.Instances);
        Assert.Equal(50, player.Events.Count(value => value == "stop"));
    }

    [Fact]
    public void DisposalRejectsLateForegroundSurfaceAndResolutionCallbacks()
    {
        var player = new Player();
        var session = new ForegroundPlaybackSession(player.Start, player.Stop);
        session.Play("live-A");
        session.Dispose();
        session.SetForeground(true);
        session.SetSurfaceReady(true);
        session.Play("late");
        session.Dispose();
        Assert.False(session.IsForeground);
        Assert.False(session.HasStream);
        Assert.Equal(new[] { "stop" }, player.Events);
    }
}
