using System.Collections.ObjectModel;
using TV_Player.Presentation;

namespace TV_Player.AvaloniaApp.ViewModels;

/// <summary>Owns channel picker items, selection and adjacent-channel navigation.</summary>
public sealed class PlayerChannelPickerViewModel : TV_Player.ObservableViewModelBase
{
    private readonly ChannelSelectionState _selection;
    public event Action<M3UInfo>? ChannelChanged;
    public ObservableCollection<ChannelPickerItem> ChannelPickerItems { get; } = new();

    private bool _channelPickerVisible;
    public bool ChannelPickerVisible
    {
        get => _channelPickerVisible;
        set => SetProperty(ref _channelPickerVisible, value);
    }

    private ChannelPickerItem? _selectedChannelPickerItem;
    private bool _updatingChannelPickerSelection;
    public ChannelPickerItem? SelectedChannelPickerItem
    {
        get => _selectedChannelPickerItem;
        set
        {
            if (SetProperty(ref _selectedChannelPickerItem, value) && value != null && !_updatingChannelPickerSelection)
                SelectChannel(value);
        }
    }


    public PlayerChannelPickerViewModel(M3UInfo currentProgram)
    {
        _selection = new ChannelSelectionState(currentProgram);
        _selection.ChannelRequested += channel =>
        {
            SyncCurrentChannel(channel);
            ChannelChanged?.Invoke(channel);
        };
    }

    public void SetChannels(IEnumerable<M3UInfo> channels)
    {
        _selection.SetChannels(channels);
        RefreshChannelPickerItems();
    }

    public void SyncCurrentChannel(M3UInfo channel)
    {
        _selection.Sync(channel);
        foreach (var item in ChannelPickerItems) item.IsCurrent = _selection.CurrentChannel != null && TV_Player.ChannelSelection.IsSameChannel(item.Channel, _selection.CurrentChannel);
        _updatingChannelPickerSelection = true;
        SelectedChannelPickerItem = ChannelPickerItems.FirstOrDefault(item => item.IsCurrent);
        _updatingChannelPickerSelection = false;
    }

    private void RefreshChannelPickerItems()
    {
        _updatingChannelPickerSelection = true;
        ChannelPickerItems.Clear();
        foreach (var program in _selection.Channels)
            ChannelPickerItems.Add(new ChannelPickerItem(program, _selection.CurrentChannel != null && TV_Player.ChannelSelection.IsSameChannel(program, _selection.CurrentChannel)));
        SelectedChannelPickerItem = ChannelPickerItems.FirstOrDefault(item => item.IsCurrent);
        _updatingChannelPickerSelection = false;
    }

    private void SelectChannel(ChannelPickerItem? item)
    {
        if (item != null) _selection.Select(item.Channel);
    }

    public void SwitchChannel(int direction) => _selection.Switch(direction);


}

public sealed class ChannelPickerItem : TV_Player.ObservableViewModelBase
{
    private bool _isCurrent;

    public M3UInfo Channel { get; }
    public string Name => Channel.Name;

    public bool IsCurrent
    {
        get => _isCurrent;
        set => SetProperty(ref _isCurrent, value);
    }

    public ChannelPickerItem(M3UInfo channel, bool isCurrent)
    {
        Channel = channel;
        _isCurrent = isCurrent;
    }
}
