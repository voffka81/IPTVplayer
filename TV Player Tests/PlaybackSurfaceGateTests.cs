using TV_Player.Playback;
using Xunit;

namespace TV_Player.Tests;

public class PlaybackSurfaceGateTests
{
    [Fact]
    public void StreamResolvedBeforeAndroidSurfaceWaitsForReadiness()
    {
        using var gate = new PlaybackSurfaceGate();
        var started = 0;
        gate.Request(() => started++);
        Assert.Equal(0, started);
        gate.SetReady(false);
        Assert.Equal(0, started);
        gate.SetReady(true);
        Assert.Equal(1, started);
    }

    [Fact]
    public void SurfaceCreatedBeforeStreamResolutionStartsImmediately()
    {
        using var gate = new PlaybackSurfaceGate();
        gate.SetReady(true);
        var started = false;
        gate.Request(() => started = true);
        Assert.True(started);
    }

    [Fact]
    public void RapidSwitchingBeforeSurfaceCreationStartsOnlyLastChannel()
    {
        using var gate = new PlaybackSurfaceGate();
        var started = new List<string>();
        gate.Request(() => started.Add("A"));
        gate.Request(() => started.Add("B"));
        gate.Request(() => started.Add("C"));
        gate.SetReady(true);
        Assert.Equal(new[] { "C" }, started);
    }

    [Fact]
    public void RepeatedLayoutAndSurfaceCallbacksDoNotRestartPlayback()
    {
        using var gate = new PlaybackSurfaceGate();
        var started = 0;
        gate.Request(() => started++);
        gate.SetReady(true);
        gate.SetReady(true);
        gate.SetReady(false);
        gate.SetReady(true);
        Assert.Equal(1, started);
    }

    [Fact]
    public void SurfaceLostDuringNavigationDefersNextChannelUntilRecreated()
    {
        using var gate = new PlaybackSurfaceGate();
        var started = new List<string>();
        gate.SetReady(true);
        gate.Request(() => started.Add("A"));
        gate.SetReady(false);
        gate.Request(() => started.Add("B"));
        Assert.Equal(new[] { "A" }, started);
        gate.SetReady(true);
        Assert.Equal(new[] { "A", "B" }, started);
    }

    [Fact]
    public void LeavingPlayerClearsQueuedPlaybackBeforeLateSurfaceCallback()
    {
        using var gate = new PlaybackSurfaceGate();
        var started = false;
        gate.Request(() => started = true);
        gate.Clear();
        gate.SetReady(true);
        Assert.False(started);
    }

    [Fact]
    public void DisposedControllerRejectsQueuedAndNewPlayback()
    {
        var gate = new PlaybackSurfaceGate();
        var started = false;
        gate.Request(() => started = true);
        gate.Dispose();
        gate.SetReady(true);
        gate.Request(() => started = true);
        Assert.False(started);
    }
}
