namespace TV_Player.Playback;

/// <summary>
/// Optional capability for engines that render to a native window.
/// The host must supply a valid, live platform window handle before PlayAsync.
/// </summary>
public interface IVideoSurfacePlaybackEngine
{
    void SetVideoOutputHandle(nint handle);
}
