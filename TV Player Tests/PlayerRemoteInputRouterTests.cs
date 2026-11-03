using TV_Player.Input;
using Xunit;

namespace TV_Player.Tests;

public class PlayerRemoteInputRouterTests
{
    [Theory]
    [InlineData(PlayerRemoteKey.Left, PlayerRemoteAction.SwitchChannel, -1)]
    [InlineData(PlayerRemoteKey.Right, PlayerRemoteAction.SwitchChannel, 1)]
    [InlineData(PlayerRemoteKey.Up, PlayerRemoteAction.OpenGuide, 0)]
    [InlineData(PlayerRemoteKey.Down, PlayerRemoteAction.OpenPicker, 0)]
    [InlineData(PlayerRemoteKey.Confirm, PlayerRemoteAction.OpenPicker, 0)]
    public void PlaybackRoutesRemoteKeys(PlayerRemoteKey key, PlayerRemoteAction action, int direction)
        => Assert.Equal(new(action, direction), PlayerRemoteInputRouter.Route(key, PlayerInputContext.Playback));

    [Theory]
    [InlineData(PlayerInputContext.ChannelPicker, PlayerRemoteAction.MovePicker)]
    [InlineData(PlayerInputContext.ProgramGuide, PlayerRemoteAction.MoveGuide)]
    public void OpenOverlayOwnsArrowsEvenWhenNativeButtonFocused(PlayerInputContext context, PlayerRemoteAction action)
    {
        foreach (var key in new[] { PlayerRemoteKey.Left, PlayerRemoteKey.Right, PlayerRemoteKey.Up, PlayerRemoteKey.Down })
            Assert.Equal(action, PlayerRemoteInputRouter.Route(key, context, true).Action);
    }

    [Fact]
    public void UnknownInputDoesNotDispatch()
    {
        Assert.Equal(PlayerRemoteAction.None, PlayerRemoteInputRouter.Route((PlayerRemoteKey)999, PlayerInputContext.ChannelPicker).Action);
        Assert.Equal(PlayerRemoteAction.None, PlayerRemoteInputRouter.Route(PlayerRemoteKey.Left, (PlayerInputContext)999).Action);
    }

    [Fact]
    public void ConfirmInPickerBelongsToNativeButton()
        => Assert.Equal(PlayerRemoteAction.None, PlayerRemoteInputRouter.Route(PlayerRemoteKey.Confirm, PlayerInputContext.ChannelPicker, true).Action);

    [Theory]
    [InlineData(PlayerInputContext.ChannelPicker, PlayerRemoteAction.CloseOverlay)]
    [InlineData(PlayerInputContext.ProgramGuide, PlayerRemoteAction.CloseOverlay)]
    [InlineData(PlayerInputContext.Playback, PlayerRemoteAction.LeavePlayer)]
    public void BackClosesOverlayBeforeLeaving(PlayerInputContext context, PlayerRemoteAction action)
        => Assert.Equal(action, PlayerRemoteInputRouter.Route(PlayerRemoteKey.Back, context, true).Action);

    [Theory]
    [InlineData(PlayerRemoteKey.Left)]
    [InlineData(PlayerRemoteKey.Right)]
    [InlineData(PlayerRemoteKey.Confirm)]
    public void FocusedNativeControlsRetainTheirKeys(PlayerRemoteKey key)
        => Assert.Equal(PlayerRemoteAction.None, PlayerRemoteInputRouter.Route(key, PlayerInputContext.Playback, true).Action);
}
