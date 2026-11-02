namespace TV_Player.Playback;

/// <summary>Rejects callbacks queued by an earlier playback request or a detached native player.</summary>
public static class PlaybackCallbackGuard
{
    public static bool IsCurrent(long queuedGeneration, long currentGeneration, object? queuedPlayer, object? currentPlayer)
        => queuedGeneration == currentGeneration
           && queuedPlayer is not null
           && ReferenceEquals(queuedPlayer, currentPlayer);
}
