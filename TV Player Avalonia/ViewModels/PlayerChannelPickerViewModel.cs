using System.Collections.ObjectModel;

namespace TV_Player.AvaloniaApp.ViewModels;

/// <summary>Owns channel picker items, selection and adjacent-channel navigation.</summary>
public sealed class PlayerChannelPickerViewModel : TV_Player.ObservableViewModelBase
{
    private M3UInfo _currentProgram;
    private List<M3UInfo> _programs = new();
    private int _currentProgramIndex;
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


    public PlayerChannelPickerViewModel(M3UInfo currentProgram) => _currentProgram = currentProgram;

    public void SetChannels(IEnumerable<M3UInfo> channels)
    {
        _programs = channels.ToList();
        _currentProgramIndex = TV_Player.ChannelSelection.FindIndex(_programs, _currentProgram);
        if (_currentProgramIndex < 0) _currentProgramIndex = 0;
        RefreshChannelPickerItems();
    }

    public void SyncCurrentChannel(M3UInfo channel)
    {
        _currentProgram = channel;
        foreach (var item in ChannelPickerItems) item.IsCurrent = TV_Player.ChannelSelection.IsSameChannel(item.Channel, _currentProgram);
        _updatingChannelPickerSelection = true;
        SelectedChannelPickerItem = ChannelPickerItems.FirstOrDefault(item => item.IsCurrent);
        _updatingChannelPickerSelection = false;
    }

    private void RefreshChannelPickerItems()
    {
        _updatingChannelPickerSelection = true;
        ChannelPickerItems.Clear();
        foreach (var program in _programs)
            ChannelPickerItems.Add(new ChannelPickerItem(program, TV_Player.ChannelSelection.IsSameChannel(program, _currentProgram)));
        SelectedChannelPickerItem = ChannelPickerItems.FirstOrDefault(item => item.IsCurrent);
        _updatingChannelPickerSelection = false;
    }

    private void SelectChannel(ChannelPickerItem? item)
    {
        if (item == null)
            return;

        var index = TV_Player.ChannelSelection.FindIndex(_programs, item.Channel);
        if (index < 0)
            return;

        if (index == _currentProgramIndex)
            return;

        _currentProgramIndex = index;
        _currentProgram = _programs[index];
        SyncCurrentChannel(_currentProgram);
        ChannelChanged?.Invoke(_currentProgram);
    }

    public void SwitchChannel(int direction)
    {
        if (_programs.Count < 2 || direction == 0)
            return;

        var nextIndex = TV_Player.ChannelSelection.NextIndex(_currentProgramIndex, _programs.Count, direction);
        _currentProgramIndex = nextIndex;
        _currentProgram = _programs[nextIndex];
        SyncCurrentChannel(_currentProgram);
        ChannelChanged?.Invoke(_currentProgram);
    }


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
