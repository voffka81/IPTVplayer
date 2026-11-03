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
}
