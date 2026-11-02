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
        Assert.Equal(PlaybackState.Resolving, session.State);
        Assert.Equal("https://example.test/live.m3u8", session.StreamUrl);
        Assert.Equal(id, session.Generation);
    }

    [Fact]
    public void OldStreamCallbacks_AreIgnoredAfterChannelSwitch()
    {
        using var session = new PlaybackSession();
        var old = session.Begin("first");
        var current = session.Begin("second");
        Assert.False(session.TryTransition(old, PlaybackState.Playing));
        Assert.True(session.TryTransition(current, PlaybackState.Buffering));
        Assert.True(session.TryTransition(current, PlaybackState.Playing));
        Assert.Equal(PlaybackState.Playing, session.State);
    }

    [Fact]
    public void Recovery_CanResumeOrFail()
    {
        using var session = new PlaybackSession();
        var id = session.Begin("stream");
        Assert.True(session.TryTransition(id, PlaybackState.Buffering));
        Assert.True(session.TryTransition(id, PlaybackState.Recovering));
        Assert.True(session.TryTransition(id, PlaybackState.Playing));
        Assert.True(session.TryTransition(id, PlaybackState.Error));
        Assert.False(session.TryTransition(id, PlaybackState.Playing));
    }

    [Fact]
    public void InvalidTransitions_AreRejected()
    {
        using var session = new PlaybackSession();
        var id = session.Begin("stream");
        Assert.False(session.TryTransition(id, PlaybackState.Ended));
        Assert.False(session.TryTransition(id, PlaybackState.Idle));
        Assert.Equal(PlaybackState.Resolving, session.State);
    }

    [Fact]
    public void Reset_InvalidatesPendingCallbacks()
    {
        using var session = new PlaybackSession();
        var id = session.Begin("stream");
        session.Reset();
        Assert.Equal(PlaybackState.Idle, session.State);
        Assert.Null(session.StreamUrl);
        Assert.False(session.TryTransition(id, PlaybackState.Playing));
    }

    [Fact]
    public void Dispose_IsIdempotentAndBlocksFurtherWork()
    {
        var session = new PlaybackSession();
        var id = session.Begin("stream");
        session.Dispose();
        session.Dispose();
        Assert.Equal(PlaybackState.Disposed, session.State);
        Assert.False(session.TryTransition(id, PlaybackState.Playing));
        Assert.Throws<ObjectDisposedException>(() => session.Begin("next"));
    }

    [Fact]
    public void StateChanged_EmitsOnlyRealTransitions()
    {
        using var session = new PlaybackSession();
        var events = new List<PlaybackState>();
        session.StateChanged += events.Add;
        var id = session.Begin("stream");
        session.TryTransition(id, PlaybackState.Buffering);
        session.TryTransition(id, PlaybackState.Buffering);
        session.TryTransition(id, PlaybackState.Playing);
        Assert.Equal(new[] { PlaybackState.Resolving, PlaybackState.Buffering, PlaybackState.Playing }, events);
    }
}
