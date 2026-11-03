using TV_Player;
using TV_Player.Presentation;
using TV_Player.AvaloniaApp.ViewModels;
using Xunit;

namespace TV_Player.Tests;

public class ChannelSelectionStateTests
{
    private static M3UInfo Channel(string id, string url = "stream") => new()
        { ProviderId = "provider", ChannelId = id, Name = id, Url = url };

    [Fact]
    public void RapidSwitchingUpdatesStateBeforeSendingRequestsAndWraps()
    {
        var channels = new[] { Channel("A"), Channel("B"), Channel("C") };
        var selection = new ChannelSelectionState(channels[0]);
        selection.SetChannels(channels);
        var requests = new List<string>();
        selection.ChannelRequested += channel =>
        {
            Assert.Same(channel, selection.CurrentChannel);
            requests.Add(channel.ChannelId!);
        };
        selection.Switch(1);
        selection.Switch(1);
        selection.Switch(1);
        selection.Switch(-1);
        Assert.Equal(new[] { "B", "C", "A", "C" }, requests);
    }

    [Fact]
    public void RefreshPreservesProviderIdentityAndOrderWithoutRestartingPlayback()
    {
        var selection = new ChannelSelectionState(Channel("A", "old"));
        var requests = 0;
        selection.ChannelRequested += _ => requests++;
        var updated = Channel("A", "new");
        var source = new List<M3UInfo> { Channel("B"), updated };
        selection.SetChannels(source);
        source.Clear();
        Assert.Same(updated, selection.CurrentChannel);
        Assert.Equal(1, selection.CurrentIndex);
        Assert.Equal(2, selection.Channels.Count);
        Assert.Equal(0, requests);
    }

    [Fact]
    public void RemovedChannelClearsSelectionAndNextRequestUsesAvailableChannel()
    {
        var selection = new ChannelSelectionState(Channel("A"));
        selection.SetChannels(new[] { Channel("B"), Channel("C") });
        Assert.Null(selection.CurrentChannel);
        Assert.Equal(-1, selection.CurrentIndex);
        Assert.False(selection.Select(Channel("missing")));
        Assert.True(selection.Switch(1));
        Assert.Equal("B", selection.CurrentChannel!.ChannelId);
        selection.SetChannels(Array.Empty<M3UInfo>());
        Assert.Null(selection.CurrentChannel);
        Assert.False(selection.Switch(-1));
    }

    [Fact]
    public void SynchronizationAndRepeatedSelectionDoNotRequestPlayback()
    {
        var a = Channel("A");
        var selection = new ChannelSelectionState(a);
        selection.SetChannels(new[] { a });
        var requests = 0;
        selection.ChannelRequested += _ => requests++;
        selection.Sync(Channel("A", "another-url"));
        Assert.False(selection.Select(a));
        Assert.False(selection.Switch(1));
        Assert.False(selection.Switch(0));
        Assert.Equal(0, requests);
    }

    [Fact]
    public void AvaloniaSyncThenNavigationUsesNewCurrentIndex()
    {
        var channels = new[] { Channel("A"), Channel("B"), Channel("C") };
        var picker = new PlayerChannelPickerViewModel(channels[0]);
        picker.SetChannels(channels);
        picker.SyncCurrentChannel(channels[1]);
        M3UInfo? requested = null;
        picker.ChannelChanged += channel => requested = channel;
        picker.SwitchChannel(1);
        Assert.Same(channels[2], requested);
        Assert.True(picker.ChannelPickerItems[2].IsCurrent);
        Assert.Same(picker.ChannelPickerItems[2], picker.SelectedChannelPickerItem);
    }
}
