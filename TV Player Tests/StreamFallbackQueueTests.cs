using TV_Player.Playback;
using Xunit;

namespace TV_Player.Tests;

public sealed class StreamFallbackQueueTests
{
    [Fact]
    public void ReturnsAlternatesInOrderAndStopsAtEnd()
    {
        var queue = new StreamFallbackQueue();
        queue.Reset(new[] { "first", "second" });
        Assert.True(queue.TryNext(out var first));
        Assert.Equal("first", first);
        Assert.True(queue.TryNext(out var second));
        Assert.Equal("second", second);
        Assert.False(queue.TryNext(out _));
        Assert.Equal(2, queue.Attempted);
    }

    [Fact]
    public void ResetDiscardsPreviousChannelAlternates()
    {
        var queue = new StreamFallbackQueue();
        queue.Reset(new[] { "old" });
        queue.TryNext(out _);
        queue.Reset(new[] { "new" });
        Assert.Equal(0, queue.Attempted);
        Assert.True(queue.TryNext(out var url));
        Assert.Equal("new", url);
        Assert.False(queue.HasNext);
    }

    [Fact]
    public void EmptyOrNullAlternatesAreSafe()
    {
        var queue = new StreamFallbackQueue();
        queue.Reset(null);
        Assert.False(queue.TryNext(out _));
        queue.Reset(Array.Empty<string>());
        Assert.False(queue.HasNext);
    }

    [Fact]
    public void ResetSnapshotsSourceList()
    {
        var source = new List<string> { "original" };
        var queue = new StreamFallbackQueue();
        queue.Reset(source);
        source[0] = "changed";
        Assert.True(queue.TryNext(out var url));
        Assert.Equal("original", url);
    }
}
