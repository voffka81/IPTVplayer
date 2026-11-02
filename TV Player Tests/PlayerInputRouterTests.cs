using TV_Player.Input;
using Xunit;

namespace TV_Player.Tests;

public class PlayerInputRouterTests
{
    [Theory]
    [InlineData(false, false, PlayerInputContext.Playback)]
    [InlineData(true, false, PlayerInputContext.ProgramGuide)]
    [InlineData(false, true, PlayerInputContext.ChannelPicker)]
    [InlineData(true, true, PlayerInputContext.ChannelPicker)]
    public void Context_RespectsVisibleOverlayPriority(bool guide, bool picker, PlayerInputContext expected)
        => Assert.Equal(expected, PlayerInputRouter.GetContext(guide, picker));

    [Theory]
    [InlineData(PlayerInputContext.Playback, PlayerInputAction.SwitchChannel)]
    [InlineData(PlayerInputContext.ProgramGuide, PlayerInputAction.MoveProgramGuide)]
    [InlineData(PlayerInputContext.ChannelPicker, PlayerInputAction.MoveChannelPicker)]
    public void HorizontalInput_RoutesToActiveContext(PlayerInputContext context, PlayerInputAction action)
    {
        Assert.Equal(new PlayerInputResult(action, 1), PlayerInputRouter.RouteHorizontal(1, context));
        Assert.Equal(new PlayerInputResult(action, -1), PlayerInputRouter.RouteHorizontal(-1, context));
    }

    [Fact]
    public void ZeroDirection_DoesNotDispatch()
        => Assert.Equal(PlayerInputResult.None, PlayerInputRouter.RouteHorizontal(0, PlayerInputContext.Playback));

    [Fact]
    public void Magnitude_IsNormalized()
        => Assert.Equal(new PlayerInputResult(PlayerInputAction.MoveProgramGuide, -1),
            PlayerInputRouter.RouteHorizontal(-50, PlayerInputContext.ProgramGuide));

    [Fact]
    public void UnknownContext_DoesNotSwitchChannels()
        => Assert.Equal(PlayerInputResult.None, PlayerInputRouter.RouteHorizontal(1, (PlayerInputContext)999));
}
