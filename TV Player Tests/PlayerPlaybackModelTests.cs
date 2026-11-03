using TV_Player.Playback;
using Xunit;

namespace TV_Player.Tests;

public sealed class PlayerPlaybackModelTests
{
    private static PlaybackToken Connect(PlayerPlaybackModel model, string url = "live")
    {
        var generation = model.Begin();
        Assert.True(model.Apply(new(PlayerPlaybackCommandKind.Connect, generation, Url: url)));
        return model.Snapshot.Token;
    }

    private static bool Native(PlayerPlaybackModel model, PlaybackToken token, PlayerPlaybackCommandKind kind, float percent = 0) =>
        model.Apply(new(kind, token.Generation, token.Epoch, Percent: percent));

    [Fact]
    public void ResolvingConnectingPlaying_ExposeOneLoadingAndControlsPolicy()
    {
        using var model = new PlayerPlaybackModel();
        Assert.Equal(PlayerPlaybackPhase.Idle, model.Snapshot.Phase);
        var generation = model.Begin();
        Assert.Equal(PlayerPlaybackPhase.Resolving, model.Snapshot.Phase);
        Assert.True(model.Snapshot.IsLoading);
        Assert.False(model.Snapshot.CanAutoHideControls);
        Assert.True(model.Apply(new(PlayerPlaybackCommandKind.Connect, generation, Url: " live ")));
        Assert.Equal("live", model.Snapshot.StreamUrl);
        var token = model.Snapshot.Token;
        Assert.True(Native(model, token, PlayerPlaybackCommandKind.Buffering, 50));
        Assert.True(model.Snapshot.IsLoading);
        Assert.True(Native(model, token, PlayerPlaybackCommandKind.Playing));
        Assert.False(model.Snapshot.IsLoading);
        Assert.True(model.Snapshot.CanAutoHideControls);
        Assert.True(Native(model, token, PlayerPlaybackCommandKind.Buffering, 20));
        Assert.True(model.Snapshot.HasStarted);
        Assert.False(model.Snapshot.IsLoading);
        Assert.True(model.Snapshot.CanAutoHideControls);
    }

    [Theory]
    [InlineData(PlayerPlaybackCommandKind.Playing)]
    [InlineData(PlayerPlaybackCommandKind.Buffering)]
    [InlineData(PlayerPlaybackCommandKind.Fail)]
    [InlineData(PlayerPlaybackCommandKind.End)]
    public void ChannelChange_RejectsAllPreviousNativeEvents(PlayerPlaybackCommandKind kind)
    {
        using var model = new PlayerPlaybackModel();
        var previous = Connect(model);
        var next = model.Begin();
        var resolving = model.Snapshot;
        Assert.False(Native(model, previous, kind));
        Assert.Equal(resolving, model.Snapshot);
        Assert.False(model.Apply(new(PlayerPlaybackCommandKind.Connect, previous.Generation, Url: "old")));
        Assert.True(model.Apply(new(PlayerPlaybackCommandKind.Connect, next, Url: "new")));
        Assert.False(Native(model, previous, kind));
        Assert.Equal("new", model.Snapshot.StreamUrl);
    }

    [Fact]
    public void SuspendWhileResolving_AcceptsProviderResultButWaitsForForegroundAndSurface()
    {
        using var model = new PlayerPlaybackModel();
        var generation = model.Begin();
        Assert.True(model.Apply(new(PlayerPlaybackCommandKind.Suspend, generation)));
        Assert.True(model.Apply(new(PlayerPlaybackCommandKind.Connect, generation, Url: "live")));
        Assert.False(model.CanStart("live"));
        Assert.False(model.Snapshot.IsLoading);
        Assert.True(model.Apply(new(PlayerPlaybackCommandKind.Resume, generation)));
        Assert.True(model.CanStart("live"));
        Assert.True(model.Snapshot.IsLoading);
    }

    [Fact]
    public void BackgroundReturn_RejectsOldNativeAttemptAndStartsFreshSurfaceOnce()
    {
        using var model = new PlayerPlaybackModel();
        var starts = new List<PlaybackToken>();
        var stops = 0;
        using var session = new ForegroundPlaybackSession(url =>
        {
            if (model.CanStart(url)) starts.Add(model.Snapshot.Token);
        }, () => stops++);
        var initial = Connect(model);
        session.SetForeground(true);
        session.Play("live");
        session.SetSurfaceReady(true);
        Native(model, initial, PlayerPlaybackCommandKind.Playing);
        model.Apply(new(PlayerPlaybackCommandKind.Suspend, initial.Generation));
        session.SetForeground(false);
        Assert.Equal(1, stops);
        Assert.False(Native(model, initial, PlayerPlaybackCommandKind.Playing));
        model.Apply(new(PlayerPlaybackCommandKind.Resume, initial.Generation));
        session.SetForeground(true);
        Assert.Single(starts);
        session.SetSurfaceReady(true);
        session.SetSurfaceReady(true);
        Assert.Equal(2, starts.Count);
        Assert.NotEqual(starts[0], starts[1]);
        Assert.True(Native(model, starts[1], PlayerPlaybackCommandKind.Playing));
        Assert.Equal(PlayerPlaybackPhase.Playing, model.Snapshot.Phase);
    }

    [Fact]
    public void NewChannelWhileBackgrounded_NeverRestartsPreviousUrl()
    {
        using var model = new PlayerPlaybackModel();
        var old = Connect(model, "old");
        model.Apply(new(PlayerPlaybackCommandKind.Suspend, old.Generation));
        var generation = model.Begin();
        model.Apply(new(PlayerPlaybackCommandKind.Resume, generation));
        Assert.False(model.CanStart("old"));
        Assert.Equal(PlayerPlaybackPhase.Resolving, model.Snapshot.Phase);
        model.Apply(new(PlayerPlaybackCommandKind.Connect, generation, Url: "new"));
        Assert.True(model.CanStart("new"));
        Assert.False(model.CanStart("old"));
    }

    [Theory]
    [InlineData(PlayerPlaybackFailure.NoStream)]
    [InlineData(PlayerPlaybackFailure.ResolutionFailed)]
    public void ProviderFailureInBackground_IsPreservedOnResume(PlayerPlaybackFailure failure)
    {
        using var model = new PlayerPlaybackModel();
        var generation = model.Begin();
        model.Apply(new(PlayerPlaybackCommandKind.Suspend, generation));
        var error = new InvalidOperationException("provider unavailable");
        Assert.True(model.Apply(new(PlayerPlaybackCommandKind.Fail, generation, Failure: failure, Cause: error)));
        model.Apply(new(PlayerPlaybackCommandKind.Resume, generation));
        Assert.Equal(PlayerPlaybackPhase.Error, model.Snapshot.Phase);
        Assert.Equal(failure, model.Snapshot.Failure);
        Assert.Same(error, model.Snapshot.Cause);
        Assert.False(model.Snapshot.IsLoading);
    }

    [Fact]
    public void Recovery_InvalidatesPriorAttemptAndAllowsAlternateUrl()
    {
        using var model = new PlayerPlaybackModel();
        var token = Connect(model);
        Native(model, token, PlayerPlaybackCommandKind.Playing);
        Assert.True(Native(model, token, PlayerPlaybackCommandKind.Recover));
        Assert.True(model.Snapshot.IsLoading);
        Assert.False(Native(model, token, PlayerPlaybackCommandKind.Fail));
        Assert.True(model.Apply(new(PlayerPlaybackCommandKind.Connect, token.Generation, Url: "alternate")));
        Assert.True(model.CanStart("alternate"));
        Assert.True(Native(model, model.Snapshot.Token, PlayerPlaybackCommandKind.Playing));
    }

    [Fact]
    public void PauseResumeAndStop_HaveExplicitTransitions()
    {
        using var model = new PlayerPlaybackModel();
        var token = Connect(model);
        Native(model, token, PlayerPlaybackCommandKind.Playing);
        Assert.True(Native(model, token, PlayerPlaybackCommandKind.Pause));
        Assert.Equal(PlayerPlaybackPhase.Paused, model.Snapshot.Phase);
        Assert.False(model.Snapshot.CanAutoHideControls);
        Assert.False(Native(model, token, PlayerPlaybackCommandKind.Buffering));
        Assert.True(model.Apply(new(PlayerPlaybackCommandKind.Resume, token.Generation)));
        Assert.True(Native(model, token, PlayerPlaybackCommandKind.Playing));
        Assert.True(model.Apply(new(PlayerPlaybackCommandKind.Stop, token.Generation)));
        Assert.Null(model.Snapshot.StreamUrl);
        Assert.False(Native(model, token, PlayerPlaybackCommandKind.Playing));
        Assert.False(model.Apply(new(PlayerPlaybackCommandKind.Connect, token.Generation, Url: "old")));
        var current = model.Snapshot.Token.Generation;
        model.Apply(new(PlayerPlaybackCommandKind.Suspend, current));
        model.Apply(new(PlayerPlaybackCommandKind.Resume, current));
        Assert.Equal(PlayerPlaybackPhase.Stopped, model.Snapshot.Phase);
        Assert.False(model.Snapshot.IsLoading);
    }

    [Theory]
    [InlineData(PlayerPlaybackCommandKind.Fail, PlayerPlaybackPhase.Error)]
    [InlineData(PlayerPlaybackCommandKind.End, PlayerPlaybackPhase.Ended)]
    public void TerminalEvents_StopLoadingAndRejectLatePlaying(PlayerPlaybackCommandKind command, PlayerPlaybackPhase phase)
    {
        using var model = new PlayerPlaybackModel();
        var token = Connect(model);
        Assert.True(Native(model, token, command));
        Assert.Equal(phase, model.Snapshot.Phase);
        Assert.False(model.Snapshot.IsLoading);
        Assert.False(Native(model, token, PlayerPlaybackCommandKind.Playing));
        Assert.False(model.Snapshot.CanAutoHideControls);
    }

    [Theory]
    [InlineData(PlaybackState.Opening, PlayerPlaybackPhase.Connecting)]
    [InlineData(PlaybackState.Buffering, PlayerPlaybackPhase.Buffering)]
    [InlineData(PlaybackState.Playing, PlayerPlaybackPhase.Playing)]
    [InlineData(PlaybackState.Paused, PlayerPlaybackPhase.Paused)]
    [InlineData(PlaybackState.Failed, PlayerPlaybackPhase.Error)]
    public void BackendStates_MapToSharedContract(PlaybackState backend, PlayerPlaybackPhase expected)
    {
        using var model = new PlayerPlaybackModel();
        var token = Connect(model);
        Assert.True(model.Apply(PlayerPlaybackCommand.FromBackend(backend, token)));
        Assert.Equal(expected, model.Snapshot.Phase);
    }

    [Fact]
    public void RepeatedLifecycleSignals_DoNotInvalidateFreshAttempt()
    {
        using var model = new PlayerPlaybackModel();
        var token = Connect(model);
        Assert.True(model.Apply(new(PlayerPlaybackCommandKind.Suspend, token.Generation)));
        var suspended = model.Snapshot.Token;
        Assert.False(model.Apply(new(PlayerPlaybackCommandKind.Suspend, token.Generation)));
        Assert.Equal(suspended, model.Snapshot.Token);
        Assert.True(model.Apply(new(PlayerPlaybackCommandKind.Resume, token.Generation)));
        Assert.False(model.Apply(new(PlayerPlaybackCommandKind.Resume, token.Generation)));
        Assert.True(Native(model, suspended, PlayerPlaybackCommandKind.Playing));
        Assert.False(Native(model, suspended, PlayerPlaybackCommandKind.Opening));
        Assert.True(model.Snapshot.HasStarted);
    }

    [Fact]
    public void Disposal_InvalidatesCallbacksAndPublishesTerminalSnapshotOnce()
    {
        var model = new PlayerPlaybackModel();
        var token = Connect(model);
        var changes = 0;
        model.Changed += _ => changes++;
        model.Dispose();
        model.Dispose();
        Assert.Equal(1, changes);
        Assert.Equal(PlayerPlaybackPhase.Disposed, model.Snapshot.Phase);
        Assert.False(model.IsCurrent(token));
        Assert.False(Native(model, token, PlayerPlaybackCommandKind.Playing));
        Assert.Throws<ObjectDisposedException>(() => model.Begin());
    }
}
