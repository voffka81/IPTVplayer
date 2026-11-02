using TV_Player.Playback;
using Xunit;

namespace TV_Player.Tests;

public sealed class PlaybackCallbackGuardTests
{
    [Fact]
    public void CurrentGenerationAndPlayer_AcceptsCallback()
    {
        var player = new object();
        Assert.True(PlaybackCallbackGuard.IsCurrent(3, 3, player, player));
    }

    [Fact]
    public void ChannelSwitch_RejectsQueuedCallback()
    {
        var player = new object();
        Assert.False(PlaybackCallbackGuard.IsCurrent(3, 4, player, player));
    }

    [Fact]
    public void ReattachedPlayer_RejectsOldNativeCallback()
    {
        Assert.False(PlaybackCallbackGuard.IsCurrent(3, 3, new object(), new object()));
    }

    [Fact]
    public void DetachedPlayer_RejectsCallback()
    {
        Assert.False(PlaybackCallbackGuard.IsCurrent(3, 3, new object(), null));
        Assert.False(PlaybackCallbackGuard.IsCurrent(3, 3, null, new object()));
    }
}
