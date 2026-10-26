using TV_Player;
using Xunit;

namespace TV_Player.Tests;

public class PlaylistParserTests
{
    [Fact]
    public async Task LocalM3u_ParsesChannelAndGroup()
    {
        var path = Path.Combine(Path.GetTempPath(), $"iptv-test-{Guid.NewGuid():N}.m3u");
        try
        {
            await File.WriteAllTextAsync(path,
                "#EXTM3U\n#EXTINF:-1 tvg-id=\"news.id\" group-title=\"News\",News One\nhttps://example.invalid/live.m3u8\n");

            var (channels, _) = await M3UParser.DownloadM3UFromWebAsync(path);

            var channel = Assert.Single(channels);
            Assert.Equal("News One", channel.Name);
            Assert.Equal("News", channel.GroupTitle);
            Assert.Equal("news.id", channel.TvgID);
            Assert.Equal("https://example.invalid/live.m3u8", channel.Url);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task EmptyLocalM3u_ReturnsNoChannels()
    {
        var path = Path.Combine(Path.GetTempPath(), $"iptv-test-{Guid.NewGuid():N}.m3u");
        try
        {
            await File.WriteAllTextAsync(path, string.Empty);
            var (channels, guide) = await M3UParser.DownloadM3UFromWebAsync(path);
            Assert.Empty(channels);
            Assert.Equal(string.Empty, guide);
        }
        finally
        {
            File.Delete(path);
        }
    }
}
