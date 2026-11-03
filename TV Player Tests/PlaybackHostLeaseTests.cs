using TV_Player.AvaloniaApp.Services;
using Xunit;

namespace TV_Player.Tests;

public sealed class PlaybackHostLeaseTests
{
    private sealed class Resource(Action release) : IDisposable { public void Dispose() => release(); }

    [Fact]
    public void ExitAndInitializationFailure_DetachHostBeforeAdapterAndPlayerOnlyOnce()
    {
        var events = new List<string>();
        using var lease = new PlaybackHostLease(() => events.Add("detach"),
            new Resource(() => events.Add("adapter")), new Resource(() => events.Add("player")));
        lease.Dispose();
        lease.Dispose(); // Failed initialization followed by view detachment.
        Assert.Equal(new[] { "detach", "adapter", "player" }, events);
    }

    [Fact]
    public void AdapterFailure_StillReleasesDetachedPlayerWithoutDoubleDisposal()
    {
        var events = new List<string>();
        var lease = new PlaybackHostLease(() => events.Add("detach"),
            new Resource(() => { events.Add("adapter"); throw new InvalidOperationException("Stop failed"); }),
            new Resource(() => events.Add("player")));
        Assert.Throws<InvalidOperationException>(() => lease.Dispose());
        lease.Dispose();
        Assert.Equal(new[] { "detach", "adapter", "player" }, events);
    }

    [Fact]
    public void FailedDetach_DoesNotFreePlayerStillReferencedByHostAndCanRetry()
    {
        var events = new List<string>();
        var fail = true;
        var lease = new PlaybackHostLease(() =>
        {
            if (fail) throw new InvalidOperationException("Host is still attached");
            events.Add("detach");
        }, new Resource(() => events.Add("adapter")), new Resource(() => events.Add("player")));
        Assert.Throws<InvalidOperationException>(() => lease.Dispose());
        Assert.Empty(events);
        Assert.False(lease.IsReleased);
        fail = false;
        lease.Dispose();
        Assert.True(lease.IsReleased);
        Assert.Equal(new[] { "detach", "adapter", "player" }, events);
    }

    [Fact]
    public void ReentrantHostCallback_CannotReleaseThePlayerTwice()
    {
        var releases = 0;
        PlaybackHostLease? lease = null;
        lease = new PlaybackHostLease(() => lease!.Dispose(),
            new Resource(() => releases++), new Resource(() => releases++));
        lease.Dispose();
        Assert.Equal(2, releases);
    }
}
