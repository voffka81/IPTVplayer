using CommunityToolkit.Mvvm.Input;
using System.Windows.Input;

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
        Programs = TVPlayerViewModel.Instance.PlayListsData
            .Select(x => new GroupInfo { Name = x.Key, Count = 0 })
            .ToList();
    }

    private void OnItemSelected(GroupInfo? group)
    {
        if (group == null)
            return;

        TVPlayerViewModel.Instance.ShowProgramsGroupScreen(group.Name);
    }
}
