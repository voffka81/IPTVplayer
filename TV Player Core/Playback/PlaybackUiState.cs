namespace TV_Player.Playback;

/// <summary>Single source of truth for the player loading indicator and overlay auto-hide readiness.</summary>
public sealed class PlaybackUiState
{
    public bool HasStarted { get; private set; }
    public bool IsBuffering { get; private set; }
    public bool CanAutoHideOverlay => HasStarted && !IsBuffering;

    public void Begin()
    {
        HasStarted = false;
        IsBuffering = true;
    }

    public void Buffering(float percent)
    {
        if (!HasStarted)
            IsBuffering = percent < 100;
    }

    public void Playing()
    {
        HasStarted = true;
        IsBuffering = false;
    }

    public void End()
    {
        HasStarted = false;
        IsBuffering = false;
    }
}
