namespace TV_Player.Input;

/// <summary>UI-framework-independent navigation context and command routing.</summary>
public enum PlayerInputContext { Playback, ProgramGuide, ChannelPicker }
public enum PlayerInputAction { None, SwitchChannel, MoveProgramGuide, MoveChannelPicker }

public readonly record struct PlayerInputResult(PlayerInputAction Action, int Direction)
{
    public static PlayerInputResult None => new(PlayerInputAction.None, 0);
}

public static class PlayerInputRouter
{
    public static PlayerInputContext GetContext(bool programGuideVisible, bool channelPickerVisible)
    {
        // Channel picker is drawn above the guide, so it owns navigation when both are visible.
        if (channelPickerVisible) return PlayerInputContext.ChannelPicker;
        if (programGuideVisible) return PlayerInputContext.ProgramGuide;
        return PlayerInputContext.Playback;
    }

    public static PlayerInputResult RouteHorizontal(int direction, PlayerInputContext context)
    {
        if (direction == 0) return PlayerInputResult.None;
        var normalized = Math.Sign(direction);
        return context switch
        {
            PlayerInputContext.ProgramGuide => new(PlayerInputAction.MoveProgramGuide, normalized),
            PlayerInputContext.ChannelPicker => new(PlayerInputAction.MoveChannelPicker, normalized),
            PlayerInputContext.Playback => new(PlayerInputAction.SwitchChannel, normalized),
            _ => PlayerInputResult.None
        };
    }
}
