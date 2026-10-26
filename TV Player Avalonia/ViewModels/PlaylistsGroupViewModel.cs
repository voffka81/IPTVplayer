using CommunityToolkit.Mvvm.Input;
using System.Windows.Input;
using TV_Player.ViewModels;

namespace TV_Player.AvaloniaApp.ViewModels;

public class PlaylistsGroupViewModel : TV_Player.ObservableViewModelBase
{
    private List<GroupInfo> _programs = new();
    public List<GroupInfo> Programs
    {
        get => _programs;
        set => SetProperty(ref _programs, value);
    }

    public ICommand SelectPlaylistCommand { get; }

    public PlaylistsGroupViewModel()
    {
        SelectPlaylistCommand = new RelayCommand<GroupInfo>(OnItemSelected);
        var playlists = TVPlayerViewModel.Instance.PlayListsData;
        var orderedNames = SettingsModel.PlaylistOrder
            .Where(playlists.ContainsKey)
            .Distinct(StringComparer.Ordinal)
            .Concat(playlists.Keys.Where(name => !SettingsModel.PlaylistOrder.Contains(name, StringComparer.Ordinal)));
        Programs = orderedNames
            .Select(name => new GroupInfo { Name = name, Count = 0 })
            .ToList();
    }

    private void OnItemSelected(GroupInfo? group)
    {
        if (group == null)
            return;

        TVPlayerViewModel.Instance.ShowProgramsGroupScreen(group.Name);
    }
}
