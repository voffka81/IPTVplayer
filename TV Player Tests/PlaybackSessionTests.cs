using TV_Player.Playback;
using Xunit;

namespace TV_Player.Tests;

public class PlaybackSessionTests
{
    [Fact]
    public void Begin_StartsNewGenerationAndNormalizesUrl()
    {
        using var session = new PlaybackSession();
        var id = session.Begin(" https://example.test/live.m3u8 ");
        Assert.Equal(PlaybackSessionState.Resolving, session.State);
        Assert.Equal("https://example.test/live.m3u8", session.StreamUrl);
        Assert.Equal(id, session.Generation);
    }

    [Fact]
    public void OldStreamCallbacks_AreIgnoredAfterChannelSwitch()
    {
        using var session = new PlaybackSession();
        var old = session.Begin("first");
        var current = session.Begin("second");
        Assert.False(session.TryTransition(old, PlaybackSessionState.Playing));
        Assert.True(session.TryTransition(current, PlaybackSessionState.Buffering));
        Assert.True(session.TryTransition(current, PlaybackSessionState.Playing));
        Assert.Equal(PlaybackSessionState.Playing, session.State);
    }

    [Fact]
    public void Recovery_CanResumeOrFail()
    {
        using var session = new PlaybackSession();
        var id = session.Begin("stream");
        Assert.True(session.TryTransition(id, PlaybackSessionState.Buffering));
        Assert.True(session.TryTransition(id, PlaybackSessionState.Recovering));
        Assert.True(session.TryTransition(id, PlaybackSessionState.Playing));
        Assert.True(session.TryTransition(id, PlaybackSessionState.Error));
        Assert.False(session.TryTransition(id, PlaybackSessionState.Playing));
    }

    [Fact]
    public void InvalidTransitions_AreRejected()
    {
        using var session = new PlaybackSession();
        var id = session.Begin("stream");
        Assert.False(session.TryTransition(id, PlaybackSessionState.Ended));
        Assert.False(session.TryTransition(id, PlaybackSessionState.Idle));
        Assert.Equal(PlaybackSessionState.Resolving, session.State);
    }

    [Fact]
    public void Reset_InvalidatesPendingCallbacks()
    {
        using var session = new PlaybackSession();
        var id = session.Begin("stream");
        session.Reset();
        Assert.Equal(PlaybackSessionState.Idle, session.State);
        Assert.Null(session.StreamUrl);
        Assert.False(session.TryTransition(id, PlaybackSessionState.Playing));
    }

    [Fact]
    public void Dispose_IsIdempotentAndBlocksFurtherWork()
    {
        var session = new PlaybackSession();
        var id = session.Begin("stream");
        session.Dispose();
        session.Dispose();
        Assert.Equal(PlaybackSessionState.Disposed, session.State);
        Assert.False(session.TryTransition(id, PlaybackSessionState.Playing));
        Assert.Throws<ObjectDisposedException>(() => session.Begin("next"));
    }

    [Fact]
    public void StateChanged_EmitsOnlyRealTransitions()
    {
        using var session = new PlaybackSession();
        var events = new List<PlaybackSessionState>();
        session.StateChanged += events.Add;
        var id = session.Begin("stream");
        session.TryTransition(id, PlaybackSessionState.Buffering);
        session.TryTransition(id, PlaybackSessionState.Buffering);
        session.TryTransition(id, PlaybackSessionState.Playing);
        Assert.Equal(new[] { PlaybackSessionState.Resolving, PlaybackSessionState.Buffering, PlaybackSessionState.Playing }, events);
    }
}
