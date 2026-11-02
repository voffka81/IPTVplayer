using TV_Player.Playback;
using Xunit;

namespace TV_Player.Tests;

public sealed class PlaybackWatchdogTests
{
    [Fact]
    public async Task FiresOnceAfterTimeout()
    {
        using var watchdog = new PlaybackWatchdog(TimeSpan.FromMilliseconds(20));
        var count = 0;
        var fired = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        watchdog.Arm(_ => { Interlocked.Increment(ref count); fired.TrySetResult(); });
        await fired.Task.WaitAsync(TimeSpan.FromSeconds(3));
        await Task.Delay(50);
        Assert.Equal(1, count);
    }

    [Fact]
    public async Task CancelPreventsTimeout()
    {
        using var watchdog = new PlaybackWatchdog(TimeSpan.FromMilliseconds(80));
        var fired = 0;
        watchdog.Arm(_ => Interlocked.Increment(ref fired));
        watchdog.Cancel();
        await Task.Delay(150);
        Assert.Equal(0, fired);
    }

    [Fact]
    public async Task RearmCancelsPreviousTimeout()
    {
        using var watchdog = new PlaybackWatchdog(TimeSpan.FromMilliseconds(70));
        var first = 0;
        var second = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        watchdog.Arm(_ => Interlocked.Increment(ref first));
        // Rearm immediately: wall-clock delays can overshoot under CI load,
        // letting the first timer fire before cancellation is requested.
        watchdog.Arm(_ => second.TrySetResult());
        await second.Task.WaitAsync(TimeSpan.FromSeconds(3));
        Assert.Equal(0, first);
    }

    [Fact]
    public void RejectsInvalidTimeoutAndDisposedArm()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new PlaybackWatchdog(TimeSpan.Zero));
        var watchdog = new PlaybackWatchdog(TimeSpan.FromSeconds(1));
        watchdog.Dispose();
        Assert.Throws<ObjectDisposedException>(() => watchdog.Arm(_ => { }));
    }
}
