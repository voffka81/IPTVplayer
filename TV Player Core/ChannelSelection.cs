namespace TV_Player;

/// <summary>Platform-independent channel identity and adjacent-channel navigation.</summary>
public static class ChannelSelection
{
    public static bool IsSameChannel(M3UInfo candidate, M3UInfo current)
    {
        if (!string.IsNullOrWhiteSpace(current.ProviderId) && !string.IsNullOrWhiteSpace(current.ChannelId))
            return candidate.ProviderId == current.ProviderId && candidate.ChannelId == current.ChannelId;

        return ReferenceEquals(candidate, current) ||
               (candidate.Name == current.Name && candidate.Url == current.Url);
    }

    public static int FindIndex(IReadOnlyList<M3UInfo> channels, M3UInfo current)
    {
        for (var i = 0; i < channels.Count; i++)
            if (IsSameChannel(channels[i], current)) return i;
        return -1;
    }

    public static int NextIndex(int currentIndex, int count, int direction)
    {
        if (count == 0 || direction == 0) return currentIndex;
        var start = currentIndex >= 0 && currentIndex < count ? currentIndex : 0;
        return (start + Math.Sign(direction) + count) % count;
    }
}
