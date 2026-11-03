using TV_Player;
using Xunit;

public sealed class ChannelSelectionTests
{
    private static M3UInfo Channel(string name, string url) => new() { Name = name, Url = url };

    [Fact]
    public void MatchesChannelByNameAndUrlWhenProviderIdentityAbsent()
    {
        Assert.True(ChannelSelection.IsSameChannel(Channel("News", "https://example.com/a"), Channel("News", "https://example.com/a")));
        Assert.False(ChannelSelection.IsSameChannel(Channel("News", "https://example.com/b"), Channel("News", "https://example.com/a")));
    }

    [Theory]
    [InlineData(0, 3, -1, 2)]
    [InlineData(2, 3, 1, 0)]
    [InlineData(1, 3, 5, 2)]
    [InlineData(1, 3, 0, 1)]
    public void NavigatesAndWraps(int current, int count, int direction, int expected)
        => Assert.Equal(expected, ChannelSelection.NextIndex(current, count, direction));

    [Fact]
    public void FindsMatchingChannel()
    {
        var channels = new[] { Channel("A", "a"), Channel("B", "b") };
        Assert.Equal(1, ChannelSelection.FindIndex(channels, Channel("B", "b")));
        Assert.Equal(-1, ChannelSelection.FindIndex(channels, Channel("C", "c")));
    }

    [Fact]
    public void ProviderIdentityTakesPriorityOverChangedStreamUrl()
    {
        var current = new M3UInfo { Name = "News", Url = "old", ProviderId = "provider", ChannelId = "channel" };
        var updated = new M3UInfo { Name = "News HD", Url = "new", ProviderId = "provider", ChannelId = "channel" };
        Assert.True(ChannelSelection.IsSameChannel(updated, current));
        Assert.Equal(0, ChannelSelection.FindIndex(new[] { updated }, current));
    }

    [Fact]
    public void ProviderIdentityDoesNotMatchDifferentChannel()
    {
        var current = new M3UInfo { ProviderId = "provider", ChannelId = "one" };
        var other = new M3UInfo { ProviderId = "provider", ChannelId = "two" };
        Assert.False(ChannelSelection.IsSameChannel(other, current));
    }
}
