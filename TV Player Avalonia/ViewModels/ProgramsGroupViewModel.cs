using CommunityToolkit.Mvvm.Input;
using System.Windows.Input;
using TV_Player.ViewModels;

namespace TV_Player.AvaloniaApp.ViewModels;

public class ProgramsGroupViewModel : TV_Player.ObservableViewModelBase, IDisposable
{
    private List<GroupInfo> _programs = new();
    private readonly IDisposable _groupInformationSubscriber;

    public List<GroupInfo> Programs
    {
        get => _programs;
        set => SetProperty(ref _programs, value);
    }

    public ICommand SelectGroupCommand { get; }

    public ProgramsGroupViewModel()
    {
        System.Diagnostics.Debug.WriteLine("[ProgramsGroupViewModel] Initializing...");
        SelectGroupCommand = new RelayCommand<GroupInfo>(OnItemSelected);
        if (TVPlayerViewModel.Instance.CurrentProgramsData == null)
        {
            System.Diagnostics.Debug.WriteLine("[ProgramsGroupViewModel] ERROR: CurrentProgramsData is null!");
            Programs = new List<GroupInfo>();
            return;
        }
        
        _groupInformationSubscriber = TVPlayerViewModel.Instance.CurrentProgramsData
            .GroupsInformation
            .Subscribe(groups =>
            {
                System.Diagnostics.Debug.WriteLine($"[ProgramsGroupViewModel.Subscribe] Received {groups.Count} groups from observable");
                // Filter hidden groups but keep all others (including "undefined")
                var filteredGroups = SettingsModel.HiddenGroups == null
                    ? groups
                    : groups.Where(g => !SettingsModel.HiddenGroups.Contains(g.Name.ToLowerInvariant())).ToList();
                
                System.Diagnostics.Debug.WriteLine($"[ProgramsGroupViewModel] Groups after filter: {string.Join(", ", filteredGroups.Select(g => $"{g.Name}({g.Count})"))}");
                Programs = filteredGroups;
            });
    }

    private void OnItemSelected(GroupInfo? group)
    {
        if (group == null)
            return;

        TVPlayerViewModel.Instance.ShowProgramsListScreen(group);
    }

    public void Dispose()
    {
        _groupInformationSubscriber.Dispose();
    }
}
