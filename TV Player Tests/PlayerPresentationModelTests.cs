using System.Collections.Concurrent;
using System.Globalization;
using TV_Player;
using TV_Player.Plugins;
using TV_Player.Playback;
using TV_Player.Presentation;
using TV_Player.Providers;
using Xunit;

namespace TV_Player.Tests;

public sealed class PlayerPresentationModelTests
{
    private static readonly DateTime Noon = new(2026, 11, 3, 12, 0, 0);
    private static M3UInfo Channel(string name, string url = "") => new()
        { Name = name, ChannelId = name, ProviderId = "provider", GroupTitle = "Group", Url = url };
    private static TaskCompletionSource<T> Pending<T>() => new(TaskCreationOptions.RunContinuationsAsynchronously);
    private static ProgramGuide Guide(string title, int start = -30, int end = 30) => new()
    {
        Programs = new() { new() { Title = title, StartTime = Noon.AddMinutes(start), EndTime = Noon.AddMinutes(end) } }
    };
    private static Task Inline(Action action) { action(); return Task.CompletedTask; }
    private static PlayerPresentationModel Model(StreamResolutionService? streams = null,
        Func<M3UInfo, Task<ProgramGuide?>>? guide = null, Func<Action, Task>? dispatch = null,
        Func<DateTime>? now = null, PlayerPresentationOptions? options = null) =>
        new(streams ?? new StreamResolutionService((IChannelProvider?)null), guide ?? (_ => Task.FromResult<ProgramGuide?>(null)),
            dispatch ?? Inline, now ?? (() => Noon), options, CultureInfo.InvariantCulture);

    [Fact]
    public async Task Open_ProjectsTitleSourceGuideAndPlaybackIntoOneSnapshot()
    {
        using var model = Model(guide: _ => Task.FromResult<ProgramGuide?>(Guide("News")));
        var channel = Channel("Channel A", "live|User-Agent=IPTV");
        model.SetChannels(new[] { channel });
        var requests = new List<ResolvedStream>();
        model.StreamRequested += requests.Add;
        await model.OpenAsync(channel);
        Assert.Same(channel, model.Snapshot.Channel);
        Assert.Equal("Channel A", model.Snapshot.ChannelTitle);
        Assert.Equal(0, model.Selection.CurrentIndex);
        Assert.Equal("live|User-Agent=IPTV", Assert.Single(requests).Url);
        Assert.Same(requests[0], model.Snapshot.Stream);
        Assert.Equal(PlayerStatus.Connecting, model.Snapshot.Status);
        Assert.True(model.Snapshot.Playback.IsLoading);
        Assert.Equal("News", model.Snapshot.Program!.Title);
        Assert.Equal(0.5, model.Snapshot.Program.Progress);
        Assert.False(model.Snapshot.IsGuideLoading);
    }

    [Fact]
    public async Task SlowGuide_DoesNotDelayStreamRequest()
    {
        var guide = Pending<ProgramGuide?>();
        using var model = Model(guide: _ => guide.Task);
        var requested = Pending<ResolvedStream>();
        model.StreamRequested += stream => requested.SetResult(stream);
        var load = model.OpenAsync(Channel("A", "live"));
        Assert.Equal("live", (await requested.Task.WaitAsync(TimeSpan.FromSeconds(5))).Url);
        Assert.False(load.IsCompleted);
        Assert.True(model.Snapshot.IsGuideLoading);
        guide.SetResult(Guide("News"));
        await load;
        Assert.Equal("News", model.Snapshot.Program!.Title);
    }

    [Fact]
    public async Task SlowStream_DoesNotDelayGuideAndTitle()
    {
        var stream = Pending<StreamInfo>();
        using var model = Model(new("provider", (_, _) => stream.Task), _ => Task.FromResult<ProgramGuide?>(Guide("News")));
        var load = model.OpenAsync(Channel("A"));
        Assert.Equal("A", model.Snapshot.ChannelTitle);
        Assert.Equal("News", model.Snapshot.Program!.Title);
        Assert.Equal(PlayerStatus.Resolving, model.Snapshot.Status);
        stream.SetResult(new("live"));
        await load;
    }

    [Fact]
    public async Task ChannelChange_ClearsOldSourceAndGuideBeforeNewRequestsComplete()
    {
        var nextStream = Pending<StreamInfo>();
        var nextGuide = Pending<ProgramGuide?>();
        using var model = Model(new("provider", (_, _) => nextStream.Task),
            channel => channel.Name == "A" ? Task.FromResult<ProgramGuide?>(Guide("Old program")) : nextGuide.Task);
        await model.OpenAsync(Channel("A", "old"));
        var load = model.OpenAsync(Channel("B"));
        Assert.Equal("B", model.Snapshot.ChannelTitle);
        Assert.Null(model.Snapshot.Stream);
        Assert.Null(model.Snapshot.Program);
        Assert.Empty(model.Snapshot.Guide.Window);
        Assert.Equal(PlayerStatus.Resolving, model.Snapshot.Status);
        nextStream.SetResult(new("new"));
        nextGuide.SetResult(Guide("New program"));
        await load;
        Assert.Equal("new", model.Snapshot.Stream!.Url);
        Assert.Equal("New program", model.Snapshot.Program!.Title);
    }

    [Fact]
    public async Task QueuedOldResults_CannotOverwriteNewChannelAfterUiDispatch()
    {
        var queue = new UiQueue();
        var guide = Pending<ProgramGuide?>();
        using var model = Model(guide: _ => guide.Task, dispatch: queue.Dispatch);
        var requests = new List<string>();
        model.StreamRequested += stream => requests.Add(stream.Url);
        var first = model.OpenAsync(Channel("A", "old"));
        var second = model.OpenAsync(Channel("B", "new"));
        await queue.RunOne(); // Old stream result was valid before being queued.
        Assert.Null(model.Snapshot.Stream);
        await queue.RunOne();
        Assert.Equal("new", model.Snapshot.Stream!.Url);
        Assert.Equal(new[] { "new" }, requests);
        model.CancelPending();
        await Task.WhenAll(first, second).WaitAsync(TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task LateGuideFromOldChannel_IsSuppressed()
    {
        var queue = new UiQueue();
        var oldGuide = Pending<ProgramGuide?>();
        using var model = Model(guide: channel => channel.Name == "A" ? oldGuide.Task : Task.FromResult<ProgramGuide?>(Guide("New")),
            dispatch: queue.Dispatch);
        var first = model.OpenAsync(Channel("A", "old"));
        await queue.RunOne();
        oldGuide.SetResult(Guide("Old"));
        await queue.WaitForWork();
        var second = model.OpenAsync(Channel("B", "new"));
        await queue.RunOne(); // Stale guide.
        Assert.Null(model.Snapshot.Program);
        await queue.RunOne(); // New guide.
        await queue.RunOne(); // New stream.
        await Task.WhenAll(first, second);
        Assert.Equal("New", model.Snapshot.Program!.Title);
        Assert.Equal("B", model.Snapshot.ChannelTitle);
    }

    [Fact]
    public async Task RepeatedGuideReload_LatestRequestWinsWithoutRestartingPlayback()
    {
        var old = Pending<ProgramGuide?>();
        var calls = 0;
        using var model = Model(guide: _ => ++calls == 1 ? old.Task : Task.FromResult<ProgramGuide?>(Guide("Latest")));
        var starts = 0;
        model.StreamRequested += _ => starts++;
        var initial = model.OpenAsync(Channel("A", "live"));
        await model.ReloadGuideAsync();
        old.SetException(new InvalidOperationException("Late EPG error"));
        await initial;
        Assert.Equal("Latest", model.Snapshot.Program!.Title);
        Assert.Null(model.Snapshot.GuideError);
        Assert.Equal(1, starts);
    }

    [Fact]
    public async Task GuideFailure_DoesNotFailVideoOrKeepLoadingGuide()
    {
        using var model = Model(guide: _ => throw new InvalidOperationException("No guide"));
        await model.OpenAsync(Channel("A", "live"));
        Assert.Equal(PlayerStatus.Connecting, model.Snapshot.Status);
        Assert.NotNull(model.Snapshot.GuideError);
        Assert.False(model.Snapshot.IsGuideLoading);
        Assert.Equal("live", model.Snapshot.Stream!.Url);
    }

    [Theory]
    [InlineData(false, PlayerStatus.NoStream)]
    [InlineData(true, PlayerStatus.ResolutionFailed)]
    public async Task ResolutionFailure_StillShowsSelectedChannelAndGuide(bool fail, PlayerStatus expected)
    {
        using var model = Model(new("provider", (_, _) => fail ? throw new InvalidOperationException("Provider error") : Task.FromResult(new StreamInfo(""))),
            _ => Task.FromResult<ProgramGuide?>(Guide("News")));
        await model.OpenAsync(Channel("A"));
        Assert.Equal(expected, model.Snapshot.Status);
        Assert.Equal("A", model.Snapshot.ChannelTitle);
        Assert.Equal("News", model.Snapshot.Program!.Title);
        Assert.Null(model.Snapshot.Stream);
        Assert.False(model.Snapshot.Playback.IsLoading);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task GuideOptions_PreserveUpcomingProgramPolicy(bool includeNext)
    {
        using var model = Model(guide: _ => Task.FromResult<ProgramGuide?>(Guide("Upcoming", 10, 60)),
            options: new(IncludeNextProgram: includeNext));
        await model.OpenAsync(Channel("A", "live"));
        Assert.Equal(includeNext, model.Snapshot.Program != null);
        Assert.Equal(includeNext, model.Snapshot.IsNextProgram);
        Assert.Equal("Upcoming", Assert.Single(model.Snapshot.Guide.Window).Title);
    }

    [Fact]
    public async Task ClockRefresh_AdvancesGuideWithoutNewSourceOrProviderRequests()
    {
        var now = Noon;
        var calls = 0;
        using var model = Model(guide: _ => { calls++; return Task.FromResult<ProgramGuide?>(Guide("News")); }, now: () => now);
        var starts = 0;
        model.StreamRequested += _ => starts++;
        await model.OpenAsync(Channel("A", "live"));
        now = Noon.AddMinutes(15);
        model.RefreshGuide();
        Assert.Equal(0.75, model.Snapshot.Program!.Progress);
        now = Noon.AddMinutes(30);
        model.RefreshGuide();
        Assert.Null(model.Snapshot.Program);
        Assert.Equal(1, calls);
        Assert.Equal(1, starts);
    }

    [Fact]
    public async Task SelectionAndChannelListSynchronization_ShareCurrentChannelWithoutExtraPlayback()
    {
        using var model = Model();
        var channels = new[] { Channel("A", "a"), Channel("B", "b") };
        model.SetChannels(channels);
        await model.OpenAsync(channels[0]);
        var starts = 0;
        model.StreamRequested += _ => starts++;
        model.SetChannels(channels.Reverse());
        Assert.Same(channels[0], model.Selection.CurrentChannel);
        Assert.Equal(0, starts);
        Assert.True(model.SwitchChannel(1));
        Assert.Same(channels[1], model.Snapshot.Channel);
        Assert.Equal("B", model.Snapshot.ChannelTitle);
        Assert.Equal(1, starts);
        Assert.False(model.SelectChannel(channels[1]));
        Assert.Equal(1, starts);
    }

    [Fact]
    public async Task ReentrantChannelCommand_DoesNotCancelNewerSourceRequest()
    {
        using var model = Model();
        Task? next = null;
        model.Changed += state =>
        {
            if (state.ChannelTitle == "A") next = model.OpenAsync(Channel("B", "new"));
        };
        await model.OpenAsync(Channel("A", "old"));
        await next!;
        Assert.Equal("B", model.Snapshot.ChannelTitle);
        Assert.Equal("new", model.Snapshot.Stream!.Url);
    }

    [Fact]
    public async Task BackgroundResolution_QueuesSourceAndKeepsCommonStatusHiddenUntilResume()
    {
        var pending = Pending<StreamInfo>();
        using var model = Model(new("provider", (_, _) => pending.Task));
        var load = model.OpenAsync(Channel("A"));
        var generation = model.Playback.Snapshot.Token.Generation;
        model.Playback.Apply(new(PlayerPlaybackCommandKind.Suspend, generation));
        pending.SetResult(new("live"));
        await load;
        Assert.Equal("live", model.Snapshot.Stream!.Url);
        Assert.Equal(PlayerStatus.None, model.Snapshot.Status);
        Assert.False(model.Playback.CanStart("live"));
        model.Playback.Apply(new(PlayerPlaybackCommandKind.Resume, generation));
        Assert.Equal(PlayerStatus.Connecting, model.Snapshot.Status);
        Assert.True(model.Playback.CanStart("live"));
    }

    [Fact]
    public async Task Disposal_SuppressesQueuedAndNonCooperativeResultsAndUnsubscribesSelection()
    {
        var queue = new UiQueue();
        var guide = Pending<ProgramGuide?>();
        var model = Model(guide: _ => guide.Task, dispatch: queue.Dispatch);
        var channels = new[] { Channel("A", "old"), Channel("B", "new") };
        model.SetChannels(channels);
        var load = model.OpenAsync(channels[0]);
        var changes = 0;
        var starts = 0;
        model.Changed += _ => changes++;
        model.StreamRequested += _ => starts++;
        model.Dispose();
        await queue.RunOne();
        guide.SetException(new InvalidOperationException("Late provider exception"));
        await load.WaitAsync(TimeSpan.FromSeconds(5));
        model.Selection.Switch(1);
        model.RefreshGuide();
        Assert.False(model.SelectChannel(channels[0]));
        Assert.Equal(0, changes);
        Assert.Equal(0, starts);
        Assert.Equal(PlayerPlaybackPhase.Disposed, model.Snapshot.Playback.Phase);
        Assert.Null(model.Snapshot.Stream);
        model.Dispose();
    }

    [Fact]
    public async Task NativePlaybackAndGuideTicks_UpdateBindingsWithoutRequestingAnotherStream()
    {
        using var model = Model(guide: _ => Task.FromResult<ProgramGuide?>(Guide("News")));
        var starts = 0;
        model.StreamRequested += _ => starts++;
        await model.OpenAsync(Channel("A", "live"));
        var token = model.Playback.Snapshot.Token;
        model.Playback.Apply(new(PlayerPlaybackCommandKind.Playing, token.Generation, token.Epoch));
        model.RefreshGuide();
        Assert.Equal(PlayerStatus.None, model.Snapshot.Status);
        Assert.True(model.Snapshot.Playback.CanAutoHideControls);
        Assert.False(model.Snapshot.Playback.IsLoading);
        model.Playback.Apply(new(PlayerPlaybackCommandKind.Fail, token.Generation, token.Epoch));
        Assert.Equal(PlayerStatus.PlaybackFailed, model.Snapshot.Status);
        Assert.False(model.Snapshot.Playback.IsLoading);
        Assert.Equal(1, starts);
    }

    [Fact]
    public async Task ProviderMetadataAndSharedPickerReachBothPresentationAndNativeAdapter()
    {
        var expires = new DateTimeOffset(Noon, TimeSpan.Zero).AddHours(1);
        using var model = Model(new("provider", (_, _) => Task.FromResult(new StreamInfo("primary",
            FallbackUrls: new[] { "alternate" }, ExpiresAt: expires))));
        using var picker = new TV_Player.AvaloniaApp.ViewModels.PlayerChannelPickerViewModel(model.Selection);
        var channels = new[] { Channel("A"), Channel("B") };
        model.SetChannels(channels);
        picker.RefreshChannels();
        await model.OpenAsync(channels[0]);
        var requested = Pending<ResolvedStream>();
        model.StreamRequested += stream => requested.SetResult(stream);
        picker.SelectedChannelPickerItem = picker.ChannelPickerItems[1];
        var stream = await requested.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal("B", model.Snapshot.ChannelTitle);
        Assert.Equal(1, model.Selection.CurrentIndex);
        Assert.True(picker.ChannelPickerItems[1].IsCurrent);
        Assert.Same(stream, model.Snapshot.Stream);
        Assert.Equal(new[] { "alternate" }, stream.FallbackUrls);
        Assert.Equal(expires, stream.ExpiresAt);
    }

    [Fact]
    public async Task ReplayedGuideReadiness_DoesNotStartConcurrentCacheLoads()
    {
        var pending = Pending<ProgramGuide?>();
        var calls = 0;
        using var model = Model(guide: _ => { calls++; return pending.Task; });
        var load = model.OpenAsync(Channel("A", "live"));
        await model.NotifyGuideAvailableAsync();
        await model.NotifyGuideAvailableAsync();
        Assert.Equal(1, calls);
        pending.SetResult(Guide("News"));
        await load;
        Assert.Equal("News", model.Snapshot.Program!.Title);
    }

    private sealed class UiQueue
    {
        private readonly ConcurrentQueue<(Action Action, TaskCompletionSource<bool> Done)> _work = new();
        private readonly SemaphoreSlim _signal = new(0);
        public Task Dispatch(Action action)
        {
            var done = Pending<bool>();
            _work.Enqueue((action, done));
            _signal.Release();
            return done.Task;
        }
        public async Task WaitForWork()
        {
            Assert.True(await _signal.WaitAsync(TimeSpan.FromSeconds(5)));
            _signal.Release();
        }
        public async Task RunOne()
        {
            Assert.True(await _signal.WaitAsync(TimeSpan.FromSeconds(5)));
            Assert.True(_work.TryDequeue(out var work));
            try { work.Action(); work.Done.SetResult(true); }
            catch (Exception ex) { work.Done.SetException(ex); throw; }
        }
    }
}
