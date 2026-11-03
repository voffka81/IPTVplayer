using TV_Player;
using TV_Player.Plugins;
using TV_Player.Providers;
using Xunit;

namespace TV_Player.Tests;

public class StreamResolutionServiceTests
{
    private static M3UInfo Channel(string id = "A", string url = "", string? provider = "provider") => new()
        { Name = id, ChannelId = id, Url = url, ProviderId = provider };
    private static TaskCompletionSource<StreamInfo> Pending() => new(TaskCreationOptions.RunContinuationsAsynchronously);

    [Fact]
    public async Task DirectUrlIncludingHeadersIsPassedThroughWithoutProviderCall()
    {
        using var service = new StreamResolutionService("provider", (_, _) => throw new Exception("Unexpected lookup"));
        const string url = "https://stream.example/live|User-Agent=IPTV&Referer=https%3A%2F%2Fexample.com";
        var result = await service.ResolveAsync(Channel(url: url));
        Assert.Equal(StreamResolutionStatus.Resolved, result.Status);
        Assert.Equal(url, result.Stream!.Url);
        Assert.Empty(result.Stream.FallbackUrls);
        Assert.True(service.IsCurrent(result));
    }

    [Theory]
    [InlineData(ProviderResolutionMode.WhenUrlMissing, "direct", 0)]
    [InlineData(ProviderResolutionMode.PreferProvider, "provider-url", 1)]
    public async Task ExistingMauiAndAvaloniaLookupPoliciesAreExplicit(ProviderResolutionMode mode, string url, int calls)
    {
        var invoked = 0;
        using var service = new StreamResolutionService("provider", (_, _) => { invoked++; return Task.FromResult(new StreamInfo("provider-url")); }, mode);
        var result = await service.ResolveAsync(Channel(url: "direct"));
        Assert.Equal(url, result.Stream!.Url);
        Assert.Equal(calls, invoked);
    }

    [Theory]
    [InlineData("other", "A")]
    [InlineData("PROVIDER", "A")]
    [InlineData("provider", "")]
    [InlineData("provider", " ")]
    public async Task MismatchedProviderOrMissingChannelIdentityCannotResolve(string provider, string id)
    {
        using var service = new StreamResolutionService("provider", (_, _) => throw new Exception("Unexpected lookup"));
        var result = await service.ResolveAsync(Channel(id, provider: provider));
        Assert.Equal(StreamResolutionStatus.NoStream, result.Status);
        Assert.True(service.IsCurrent(result));
    }

    [Fact]
    public async Task ProviderlessServiceStillSupportsM3u()
    {
        using var service = new StreamResolutionService((IChannelProvider?)null);
        Assert.Equal("direct", (await service.ResolveAsync(Channel(url: "direct", provider: null))).Stream!.Url);
        Assert.Equal(StreamResolutionStatus.NoStream, (await service.ResolveAsync(Channel(provider: null))).Status);
    }

    [Fact]
    public async Task ProviderFallbackOrderAndExpiryArePreservedAsSnapshot()
    {
        var fallbacks = new List<string> { "backup-B", "backup-A" };
        var expires = DateTimeOffset.Parse("2026-11-03T18:00:00Z");
        using var service = new StreamResolutionService("provider", (_, _) => Task.FromResult(new StreamInfo("primary", fallbacks, expires)));
        var result = await service.ResolveAsync(Channel());
        fallbacks.Clear();
        Assert.Equal("primary", result.Stream!.Url);
        Assert.Equal(new[] { "backup-B", "backup-A" }, result.Stream.FallbackUrls);
        Assert.Equal(expires, result.Stream.ExpiresAt);
    }

    [Fact]
    public async Task EmptyProviderResultIsStructuredNoStream()
    {
        using var service = new StreamResolutionService("provider", (_, _) => Task.FromResult(new StreamInfo(" ")));
        var result = await service.ResolveAsync(Channel());
        Assert.Equal(StreamResolutionStatus.NoStream, result.Status);
        Assert.Null(result.Stream);
        Assert.Null(result.Error);
    }

    [Fact]
    public async Task ProviderFailurePreservesCauseWithoutGeneratingUiMessage()
    {
        var error = new IOException("offline");
        using var service = new StreamResolutionService("provider", (_, _) => throw error);
        var result = await service.ResolveAsync(Channel());
        Assert.Equal(StreamResolutionStatus.ProviderFailed, result.Status);
        Assert.Same(error, result.Error);
        Assert.True(service.IsCurrent(result));
    }

    [Fact]
    public async Task ProviderSelfCancellationWithoutRequestCancellationIsFailure()
    {
        using var service = new StreamResolutionService("provider", (_, _) => Task.FromException<StreamInfo>(new OperationCanceledException()));
        Assert.Equal(StreamResolutionStatus.ProviderFailed, (await service.ResolveAsync(Channel())).Status);
    }

    [Fact]
    public async Task NewRequestCancelsOldEvenWhenProviderIgnoresCancellationAndCompletesOutOfOrder()
    {
        var old = Pending();
        var latest = Pending();
        CancellationToken oldToken = default;
        using var service = new StreamResolutionService("provider", (id, token) =>
        {
            if (id == "A") { oldToken = token; return old.Task; }
            return latest.Task;
        });
        var a = service.ResolveAsync(Channel("A"));
        var b = service.ResolveAsync(Channel("B"));
        Assert.True(oldToken.IsCancellationRequested);
        latest.SetResult(new StreamInfo("B-url"));
        var bResult = await b;
        Assert.Equal("B-url", bResult.Stream!.Url);
        Assert.True(service.IsCurrent(bResult));
        Assert.Equal(StreamResolutionStatus.Superseded, (await a).Status);
        old.SetResult(new StreamInfo("late-A"));
        Assert.True(service.IsCurrent(bResult));
    }

    [Fact]
    public async Task LateProviderFailureCannotReplaceNewDirectStream()
    {
        var old = Pending();
        using var service = new StreamResolutionService("provider", (_, _) => old.Task);
        var pending = service.ResolveAsync(Channel());
        var latest = await service.ResolveAsync(Channel("B", "direct"));
        old.SetException(new IOException("late error"));
        var stale = await pending;
        Assert.Equal(StreamResolutionStatus.Superseded, stale.Status);
        Assert.Null(stale.Error);
        Assert.True(service.IsCurrent(latest));
    }

    [Fact]
    public async Task CancelReturnsWithoutWaitingForNonCooperativeProviderAndAllowsRetry()
    {
        var old = Pending();
        using var service = new StreamResolutionService("provider", (_, _) => old.Task);
        var pending = service.ResolveAsync(Channel());
        service.Cancel();
        var cancelled = await pending;
        Assert.Equal(StreamResolutionStatus.Cancelled, cancelled.Status);
        Assert.False(service.IsCurrent(cancelled));
        var retry = await service.ResolveAsync(Channel(url: "retry"));
        Assert.True(service.IsCurrent(retry));
        old.SetResult(new StreamInfo("ignored"));
    }

    [Fact]
    public async Task ExternalCancellationInvalidatesAnAlreadyCompletedResultQueuedForUi()
    {
        using var cancellation = new CancellationTokenSource();
        using var service = new StreamResolutionService((IChannelProvider?)null);
        var result = await service.ResolveAsync(Channel(url: "direct"), cancellation.Token);
        Assert.True(service.IsCurrent(result));
        cancellation.Cancel();
        Assert.False(service.IsCurrent(result));
    }

    [Fact]
    public async Task AlreadyCancelledRequestDoesNotCallProvider()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        using var service = new StreamResolutionService("provider", (_, _) => throw new Exception("Unexpected lookup"));
        Assert.Equal(StreamResolutionStatus.Cancelled, (await service.ResolveAsync(Channel(), cancellation.Token)).Status);
    }

    [Fact]
    public async Task UiDispatchMustRejectAnEarlierSuccessfulResultAfterSelectionChanges()
    {
        using var service = new StreamResolutionService((IChannelProvider?)null);
        var old = await service.ResolveAsync(Channel(url: "A"));
        var current = await service.ResolveAsync(Channel(url: "B"));
        Assert.False(service.IsCurrent(old));
        Assert.True(service.IsCurrent(current));
        service.Cancel();
        Assert.False(service.IsCurrent(current));
    }

    [Fact]
    public async Task DisposalCancelsInFlightRequestAndRejectsFutureRequestsAndUiPublication()
    {
        var provider = Pending();
        var service = new StreamResolutionService("provider", (_, _) => provider.Task);
        var pending = service.ResolveAsync(Channel());
        service.Dispose();
        Assert.Equal(StreamResolutionStatus.Disposed, (await pending).Status);
        Assert.Equal(StreamResolutionStatus.Disposed, (await service.ResolveAsync(Channel(url: "late"))).Status);
        provider.SetResult(new StreamInfo("late"));
        service.Dispose();
    }

    [Fact]
    public async Task ThrowingCancellationCallbackDoesNotBreakNewSelection()
    {
        var pending = Pending();
        using var service = new StreamResolutionService("provider", (_, token) =>
        {
            token.Register(() => throw new InvalidOperationException("Provider callback failed"));
            return pending.Task;
        });
        var old = service.ResolveAsync(Channel());
        var current = await service.ResolveAsync(Channel(url: "direct"));
        Assert.True(service.IsCurrent(current));
        Assert.Equal(StreamResolutionStatus.Superseded, (await old).Status);
        pending.SetResult(new StreamInfo("late"));
    }

    [Fact]
    public async Task InjectedProviderIsNotOwnedOrDisposedByResolutionService()
    {
        using var provider = new Provider();
        var service = new StreamResolutionService(provider);
        var result = await service.ResolveAsync(Channel());
        Assert.Equal("resolved:A", result.Stream!.Url);
        service.Dispose();
        Assert.False(service.IsCurrent(result));
        Assert.False(provider.Disposed);
    }

    private sealed class Provider : IChannelProvider, IDisposable
    {
        public string Id => "provider";
        public string Name => "Fake";
        public Version Version => new(1, 0);
        public bool Disposed { get; private set; }
        public Task<IReadOnlyList<ChannelInfo>> GetChannelsAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<ChannelInfo>>(Array.Empty<ChannelInfo>());
        public Task<StreamInfo> ResolveStreamAsync(string channelId, CancellationToken cancellationToken = default)
            => Task.FromResult(new StreamInfo("resolved:" + channelId));
        public void Dispose() => Disposed = true;
    }
}
