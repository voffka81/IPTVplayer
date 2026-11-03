using TV_Player;
using TV_Player.Plugins;
using TV_Player.Playback;
using TV_Player.Presentation;
using TV_Player.Providers;
using Xunit;

namespace TV_Player.Tests;

/// <summary>Shared contracts composed as the Android adapter uses them; no VLC or UI framework.</summary>
public sealed class PlayerIntegrationRegressionTests
{
    private static readonly DateTime Now = new(2026, 11, 3, 12, 0, 0);
    private static M3UInfo Channel(string name, string url = "") => new()
        { Name = name, ChannelId = name, ProviderId = "provider", Url = url };
    private static TaskCompletionSource<T> Pending<T>() => new(TaskCreationOptions.RunContinuationsAsynchronously);
    private static ProgramGuide Guide(string title) => new()
    {
        Programs = new() { new() { Title = title, StartTime = Now.AddMinutes(-30), EndTime = Now.AddMinutes(30) } }
    };

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task FirstOpen_StartsOneCorrectStreamForEitherSurfaceOrdering(bool surfaceFirst)
    {
        using var player = new Player(guide: channel => Task.FromResult<ProgramGuide?>(Guide(channel.Name)));
        player.Foreground(true);
        if (surfaceFirst) player.Surface(true);
        await player.Model.OpenAsync(Channel("A", "live-A"));
        if (!surfaceFirst)
        {
            Assert.Empty(player.Starts);
            player.Surface(true);
        }
        player.Surface(true);
        var started = Assert.Single(player.Starts);
        Assert.Equal("live-A", started.Url);
        Assert.Equal("A", player.Model.Snapshot.ChannelTitle);
        Assert.Equal("A", player.Model.Snapshot.Program!.Title);
        Assert.True(player.Native(started.Token, PlayerPlaybackCommandKind.Playing));
        Assert.False(player.Model.Snapshot.Playback.IsLoading);
    }

    [Fact]
    public async Task RapidSwitchAndBackground_OnlyNewestChannelStartsOnFreshSurface()
    {
        var b = Pending<StreamInfo>();
        var c = Pending<StreamInfo>();
        using var player = new Player(new("provider", (id, _) => id == "B" ? b.Task : c.Task),
            channel => Task.FromResult<ProgramGuide?>(Guide(channel.Name)));
        player.Foreground(true);
        player.Surface(true);
        await player.Model.OpenAsync(Channel("A", "live-A"));
        var oldAttempt = Assert.Single(player.Starts).Token;
        player.Native(oldAttempt, PlayerPlaybackCommandKind.Playing);
        var loadB = player.Model.OpenAsync(Channel("B"));
        var loadC = player.Model.OpenAsync(Channel("C"));
        player.Foreground(false);
        Assert.False(player.Streaming);
        c.SetResult(new("live-C"));
        b.SetResult(new("late-B"));
        await Task.WhenAll(loadB, loadC).WaitAsync(TimeSpan.FromSeconds(5));
        player.Surface(true); // Late callback while still inactive.
        Assert.Single(player.Starts);
        Assert.Equal("C", player.Model.Snapshot.ChannelTitle);
        Assert.Equal("C", player.Model.Snapshot.Program!.Title);
        Assert.False(player.Native(oldAttempt, PlayerPlaybackCommandKind.Playing));
        Assert.False(player.Native(oldAttempt, PlayerPlaybackCommandKind.Fail));
        player.Foreground(true);
        Assert.False(player.Streaming);
        player.Surface(true);
        Assert.Equal(new[] { "live-A", "live-C" }, player.Starts.Select(start => start.Url));
        Assert.True(player.Native(player.Starts[^1].Token, PlayerPlaybackCommandKind.Playing));
        Assert.Equal(PlayerStatus.None, player.Model.Snapshot.Status);
        Assert.Equal(1, player.Stops);
    }

    [Fact]
    public async Task FailedSelectedChannel_CannotRestartPreviousUrlWhenReturningFromBackground()
    {
        using var player = new Player(new("provider", (_, _) => throw new InvalidOperationException("Unavailable")));
        player.Foreground(true);
        player.Surface(true);
        await player.Model.OpenAsync(Channel("A", "live-A"));
        var oldAttempt = player.Starts[0].Token;
        player.Native(oldAttempt, PlayerPlaybackCommandKind.Playing);
        player.Foreground(false);
        await player.Model.OpenAsync(Channel("B"));
        player.Foreground(true);
        player.Surface(true);
        Assert.Single(player.Starts);
        Assert.False(player.Streaming);
        Assert.Equal("B", player.Model.Snapshot.ChannelTitle);
        Assert.Equal(PlayerStatus.ResolutionFailed, player.Model.Snapshot.Status);
        Assert.False(player.Native(oldAttempt, PlayerPlaybackCommandKind.Playing));
        await player.Model.OpenAsync(Channel("C", "live-C"));
        Assert.Equal(new[] { "live-A", "live-C" }, player.Starts.Select(start => start.Url));
    }

    [Fact]
    public async Task ExitWhileConnecting_CancelsRequestsAndCannotAffectReenteredPlayer()
    {
        var stream = Pending<StreamInfo>();
        var guide = Pending<ProgramGuide?>();
        var cancelled = Pending<bool>();
        var old = new Player(new("provider", (_, token) =>
        {
            token.Register(() => cancelled.TrySetResult(true));
            return stream.Task;
        }), _ => guide.Task);
        old.Foreground(true);
        old.Surface(true);
        var pendingLoad = old.Model.OpenAsync(Channel("Old"));
        old.Dispose();
        Assert.True(await cancelled.Task.WaitAsync(TimeSpan.FromSeconds(5)));
        using var current = new Player();
        current.Foreground(true);
        current.Surface(true);
        await current.Model.OpenAsync(Channel("New", "live-new"));
        stream.SetResult(new("late-old"));
        guide.SetException(new InvalidOperationException("Late guide failure"));
        await pendingLoad.WaitAsync(TimeSpan.FromSeconds(5));
        old.Foreground(true);
        old.Surface(true);
        Assert.Empty(old.Starts);
        Assert.False(old.Streaming);
        Assert.Equal("live-new", Assert.Single(current.Starts).Url);
        Assert.Equal("New", current.Model.Snapshot.ChannelTitle);
    }

    [Fact]
    public async Task GuideTicksAndPickerBrowsing_DoNotStartStreamsOrHideOpenOverlay()
    {
        var now = Now;
        using var player = new Player(guide: channel => Task.FromResult<ProgramGuide?>(Guide(channel.Name)), now: () => now);
        using var ui = new PlayerUiController();
        var start = new DateTimeOffset(Now, TimeSpan.Zero);
        var channels = new[] { Channel("A", "live-A"), Channel("B", "live-B") };
        player.Model.SetChannels(channels);
        player.Foreground(true);
        player.Surface(true);
        await player.Model.OpenAsync(channels[0]);
        ui.Resume(start);
        ui.TogglePicker(start);
        now = Now.AddMinutes(15);
        player.Model.RefreshGuide();
        ui.Tick(start.AddMinutes(15));
        Assert.True(ui.State.ControlsVisible);
        Assert.True(ui.State.ChannelPickerVisible);
        Assert.Equal(0.75, player.Model.Snapshot.Program!.Progress);
        Assert.Single(player.Starts);
        Assert.Same(channels[0], player.Model.Selection.CurrentChannel);
        ui.CloseOverlay(start.AddMinutes(15));
        Assert.True(player.Model.SelectChannel(channels[1]));
        Assert.Equal("live-B", player.Starts[^1].Url);
        ui.Tick(start.AddMinutes(15).Add(PlayerUiController.AutoHideDelay));
        Assert.False(ui.State.ControlsVisible);
        Assert.Equal(2, player.Starts.Count);
    }

    [Fact]
    public async Task RepeatedSessions_HaveOneResumePerCycleAndNoPlaybackAfterDisposal()
    {
        var player = new Player();
        player.Foreground(true);
        player.Surface(true);
        for (var cycle = 0; cycle < 20; cycle++)
        {
            var url = "live-" + cycle;
            await player.Model.OpenAsync(Channel("Channel " + cycle, url));
            Assert.Equal(url, player.Starts[^1].Url);
            var stale = player.Starts[^1].Token;
            player.Foreground(false);
            player.Foreground(false);
            Assert.False(player.Streaming);
            player.Foreground(true);
            player.Foreground(true);
            Assert.False(player.Streaming);
            player.Surface(true);
            player.Surface(true);
            Assert.False(player.Native(stale, PlayerPlaybackCommandKind.Playing));
            Assert.True(player.Native(player.Starts[^1].Token, PlayerPlaybackCommandKind.Playing));
            Assert.Equal(2 * (cycle + 1), player.Starts.Count);
            Assert.Equal(cycle + 1, player.Stops);
        }
        var finalAttempt = player.Starts[^1].Token;
        player.Dispose();
        player.Foreground(true);
        player.Surface(true);
        await player.Model.OpenAsync(Channel("Late", "late"));
        Assert.False(player.Native(finalAttempt, PlayerPlaybackCommandKind.Playing));
        Assert.False(player.Streaming);
        Assert.Equal(40, player.Starts.Count);
        Assert.Equal(21, player.Stops);
        player.Dispose();
        Assert.Equal(21, player.Stops);
    }

    private sealed class Player : IDisposable
    {
        private readonly ForegroundPlaybackSession _surface;
        public readonly List<(string Url, PlaybackToken Token)> Starts = new();
        public PlayerPresentationModel Model { get; }
        public bool Streaming { get; private set; }
        public int Stops { get; private set; }
        private bool _disposed;

        public Player(StreamResolutionService? streams = null, Func<M3UInfo, Task<ProgramGuide?>>? guide = null,
            Func<DateTime>? now = null)
        {
            Model = new(streams ?? new StreamResolutionService((IChannelProvider?)null),
                guide ?? (_ => Task.FromResult<ProgramGuide?>(null)),
                action => { action(); return Task.CompletedTask; }, now ?? (() => Now));
            _surface = new ForegroundPlaybackSession(url =>
            {
                if (!Model.Playback.CanStart(url)) return;
                Streaming = true;
                Starts.Add((url, Model.Playback.Snapshot.Token));
            }, () => { Streaming = false; Stops++; });
            Model.StreamRequested += OnStream;
        }
        private void OnStream(ResolvedStream stream) => _surface.Play(stream.Url);
        public void Foreground(bool active)
        {
            Model.Playback.Apply(new(active ? PlayerPlaybackCommandKind.Resume : PlayerPlaybackCommandKind.Suspend,
                Model.Playback.Snapshot.Token.Generation));
            _surface.SetForeground(active);
        }
        public void Surface(bool ready) => _surface.SetSurfaceReady(ready);
        public bool Native(PlaybackToken token, PlayerPlaybackCommandKind command) =>
            Model.Playback.Apply(new(command, token.Generation, token.Epoch));
        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            Model.StreamRequested -= OnStream;
            Model.Dispose();
            _surface.Dispose();
        }
    }
}
