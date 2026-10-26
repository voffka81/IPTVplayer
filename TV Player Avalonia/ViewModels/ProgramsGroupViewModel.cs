using CommunityToolkit.Mvvm.Input;
using Avalonia.Threading;
using System.Windows.Input;
using TV_Player.ViewModels;

namespace TV_Player.AvaloniaApp.ViewModels;

public class ProgramsGroupViewModel : TV_Player.ObservableViewModelBase, IDisposable
{
    private List<GroupInfo> _programs = new();
    private List<GroupInfo> _allGroups = new();
    private readonly IDisposable _groupInformationSubscriber;

    public List<GroupInfo> Programs
    {
        get => _programs;
        set => SetProperty(ref _programs, value);
    }

    public ICommand SelectGroupCommand { get; }

    private string _searchText = string.Empty;
    public string SearchText
    {
        get => _searchText;
        set
        {
            if (SetProperty(ref _searchText, value))
                ApplyFilter();
        }
    }

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
                _allGroups = SettingsModel.HiddenGroups == null
                    ? groups
                    : groups.Where(g => !SettingsModel.HiddenGroups.Contains(g.Name.ToLowerInvariant())).ToList();
                
                Dispatcher.UIThread.Post(ApplyFilter);
            });
    }

    private void ApplyFilter()
    {
        var query = SearchText.Trim();
        Programs = string.IsNullOrEmpty(query)
            ? _allGroups
            : _allGroups.Where(group => group.Name.Contains(query, StringComparison.CurrentCultureIgnoreCase)).ToList();

        System.Diagnostics.Debug.WriteLine($"[ProgramsGroupViewModel] Groups after filter: {string.Join(", ", Programs.Select(g => $"{g.Name}({g.Count})"))}");
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
