using System.Reactive.Linq;
using System.Reactive.Threading.Tasks;
using TV_Player.Plugins;
using Xunit;
namespace TV_Player.Tests;
public class SourceLoadingTests
{
    [Fact]
    public async Task TemporaryFailureRetriesAndPublishesGroups()
    {
        var provider = new TestProvider(true);
        using var source = new ProgramsData("Test", provider);
        var groups = await source.GroupsInformation.FirstAsync().ToTask().WaitAsync(TimeSpan.FromSeconds(10));
        Assert.Single(groups);
        Assert.Equal(2, provider.Attempts);
    }
    [Fact]
    public async Task ConfigurationFailureReportsErrorWithoutRetry()
    {
        var provider = new TestProvider(false);
        using var source = new ProgramsData("Test", provider);
        var error = await source.LoadErrors.FirstAsync().ToTask().WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Contains("Invalid configuration", error);
        Assert.Equal(1, provider.Attempts);
    }
    private sealed class TestProvider(bool transient) : IChannelProvider
    {
        public int Attempts;
        public string Id => "test";
        public string Name => "Test";
        public Version Version => new(1, 0);
        public Task<IReadOnlyList<ChannelInfo>> GetChannelsAsync(CancellationToken cancellationToken = default)
        {
            Attempts++;
            if (!transient) throw new InvalidOperationException("Invalid configuration");
            if (Attempts == 1) throw new System.Net.Http.HttpRequestException("Temporary network failure");
            return Task.FromResult<IReadOnlyList<ChannelInfo>>(new[] { new ChannelInfo("1", "Channel", "Group") });
        }
        public Task<StreamInfo> ResolveStreamAsync(string id, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
    }
}
