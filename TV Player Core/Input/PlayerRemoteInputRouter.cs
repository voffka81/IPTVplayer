namespace TV_Player.Input;

public enum PlayerRemoteKey { Left, Right, Up, Down, Confirm, Back }
public enum PlayerRemoteAction { None, SwitchChannel, MovePicker, MoveGuide, OpenPicker, OpenGuide, CloseOverlay, LeavePlayer }
public readonly record struct PlayerRemoteResult(PlayerRemoteAction Action, int Direction = 0);

public static class PlayerRemoteInputRouter
{
    public static PlayerRemoteResult Route(PlayerRemoteKey key, PlayerInputContext context, bool nativeControlFocused = false)
    {
        if (!Enum.IsDefined(key) || !Enum.IsDefined(context)) return new(PlayerRemoteAction.None);
        if (key == PlayerRemoteKey.Back)
            return new(context == PlayerInputContext.Playback ? PlayerRemoteAction.LeavePlayer : PlayerRemoteAction.CloseOverlay);
        if (context == PlayerInputContext.Playback && nativeControlFocused)
            return new(PlayerRemoteAction.None);
        var direction = key is PlayerRemoteKey.Left or PlayerRemoteKey.Up ? -1 : 1;
        if (context == PlayerInputContext.ChannelPicker)
            // Confirm belongs to the native focused button, including Android's key-up click.
            return new(key == PlayerRemoteKey.Confirm ? PlayerRemoteAction.None : PlayerRemoteAction.MovePicker, direction);
        if (context == PlayerInputContext.ProgramGuide)
            return new(key == PlayerRemoteKey.Confirm ? PlayerRemoteAction.CloseOverlay : PlayerRemoteAction.MoveGuide, direction);
        return key switch
        {
            PlayerRemoteKey.Left => new(PlayerRemoteAction.SwitchChannel, -1),
            PlayerRemoteKey.Right => new(PlayerRemoteAction.SwitchChannel, 1),
            PlayerRemoteKey.Up => new(PlayerRemoteAction.OpenGuide),
            PlayerRemoteKey.Down or PlayerRemoteKey.Confirm => new(PlayerRemoteAction.OpenPicker),
            _ => new(PlayerRemoteAction.None)
        };
    }
}
