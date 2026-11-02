using TV_Player.Playback;
using Xunit;

namespace TV_Player.Tests;

public sealed class PlaybackUiStateTests
{
    [Fact]
    public void Begin_EnablesBufferingAndPreventsAutoHide()
    {
        var state = new PlaybackUiState();
        state.Begin();
        Assert.True(state.IsBuffering);
        Assert.False(state.CanAutoHideOverlay);
    }

    [Fact]
    public void Playing_ClearsBufferingAndAllowsAutoHide()
    {
        var state = new PlaybackUiState();
        state.Begin();
        state.Playing();
        Assert.False(state.IsBuffering);
        Assert.True(state.CanAutoHideOverlay);
    }

    [Fact]
    public void LateBufferingEvent_DoesNotReenableSpinner()
    {
        var state = new PlaybackUiState();
        state.Begin();
        state.Playing();
        state.Buffering(10);
        Assert.False(state.IsBuffering);
        Assert.True(state.CanAutoHideOverlay);
    }

    [Fact]
    public void RapidSwitch_ResetsAndRestoresUiState()
    {
        var state = new PlaybackUiState();
        state.Begin();
        state.Playing();
        state.Begin();
        Assert.True(state.IsBuffering);
        Assert.False(state.CanAutoHideOverlay);
        state.Playing();
        Assert.False(state.IsBuffering);
        Assert.True(state.CanAutoHideOverlay);
    }

    [Fact]
    public void End_ClearsBufferingAndDisablesAutoHide()
    {
        var state = new PlaybackUiState();
        state.Begin();
        state.End();
        Assert.False(state.IsBuffering);
        Assert.False(state.CanAutoHideOverlay);
    }
}
