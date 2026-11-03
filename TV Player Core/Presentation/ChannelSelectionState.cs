namespace TV_Player.Presentation;

/// <summary>Shared ordered channel selection. Synchronization never requests playback.</summary>
public sealed class ChannelSelectionState
{
    public IReadOnlyList<M3UInfo> Channels { get; private set; } = Array.Empty<M3UInfo>();
    public M3UInfo? CurrentChannel { get; private set; }
    public int CurrentIndex => CurrentChannel == null ? -1 : ChannelSelection.FindIndex(Channels, CurrentChannel);
    public event Action<M3UInfo>? ChannelRequested;

    public ChannelSelectionState(M3UInfo? current = null) => CurrentChannel = current;

    public void SetChannels(IEnumerable<M3UInfo> channels)
    {
        Channels = Array.AsReadOnly(channels.ToArray());
        if (CurrentChannel != null) Sync(CurrentChannel);
    }

    public void Sync(M3UInfo channel)
    {
        var index = ChannelSelection.FindIndex(Channels, channel);
        CurrentChannel = index < 0 ? null : Channels[index];
    }

    public bool Select(M3UInfo channel)
    {
        var index = ChannelSelection.FindIndex(Channels, channel);
        if (index < 0 || index == CurrentIndex) return false;
        CurrentChannel = Channels[index];
        ChannelRequested?.Invoke(CurrentChannel);
        return true;
    }

    public bool Switch(int direction)
    {
        if (Channels.Count == 0 || direction == 0) return false;
        var current = CurrentIndex;
        var next = current < 0 ? (direction > 0 ? 0 : Channels.Count - 1)
            : ChannelSelection.NextIndex(current, Channels.Count, direction);
        return Select(Channels[next]);
    }
}
