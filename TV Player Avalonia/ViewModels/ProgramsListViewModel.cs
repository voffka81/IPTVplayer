using System.Collections.ObjectModel;
using System.Windows.Input;
using CommunityToolkit.Mvvm.Input;
using Avalonia.Threading;

namespace TV_Player.AvaloniaApp.ViewModels;

public class ProgramsListViewModel : TV_Player.ObservableViewModelBase, IDisposable
{
    private readonly IDisposable _programSubscription;
    private ObservableCollection<M3UInfo> _programs = new();
    private List<M3UInfo> _allPrograms = new();
    private CancellationTokenSource? _filterCancellation;

    public ObservableCollection<M3UInfo> Programs
    {
        get => _programs;
        set => SetProperty(ref _programs, value);
    }

    public ICommand SelectProgramCommand { get; }

    private string _searchText = string.Empty;
    public string SearchText
    {
        get => _searchText;
        set
        {
            if (SetProperty(ref _searchText, value))
                ScheduleFilter();
        }
    }

    public ProgramsListViewModel(GroupInfo groupInfo)
    {
        SelectProgramCommand = new RelayCommand<M3UInfo>(OnItemSelected);
        _programSubscription = TVPlayerViewModel.Instance.CurrentProgramsData!
            .AllPrograms
            .Subscribe(newPrograms =>
            {
                _allPrograms = newPrograms
                    .Where(p => p.GroupTitle == groupInfo.Name && (!string.IsNullOrWhiteSpace(p.Url) || (!string.IsNullOrWhiteSpace(p.ProviderId) && !string.IsNullOrWhiteSpace(p.ChannelId))))
                    .ToList();

                Dispatcher.UIThread.Post(() =>
                {
                    ScheduleFilter();
                });
            });
    }

    private void ScheduleFilter()
    {
        _filterCancellation?.Cancel();
        _filterCancellation?.Dispose();
        var cancellation = _filterCancellation = new CancellationTokenSource();
        _ = ApplyFilterAfterDelayAsync(cancellation.Token);
    }

    private async Task ApplyFilterAfterDelayAsync(CancellationToken cancellationToken)
    {
        try
        {
            await Task.Delay(180, cancellationToken);
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                if (!cancellationToken.IsCancellationRequested)
                    ApplyFilter();
            });
        }
        catch (OperationCanceledException) { }
    }

    private void ApplyFilter()
    {
        var query = SearchText.Trim();
        var filteredPrograms = string.IsNullOrEmpty(query)
            ? _allPrograms
            : _allPrograms.Where(program => program.Name.Contains(query, StringComparison.CurrentCultureIgnoreCase)).ToList();

        Programs = new ObservableCollection<M3UInfo>(filteredPrograms);
    }

    private void OnItemSelected(M3UInfo? program)
    {
        if (program == null)
            return;

        TVPlayerViewModel.Instance.ShowPlayerScreen(program);
    }

    public void Dispose()
    {
        _filterCancellation?.Cancel();
        _filterCancellation?.Dispose();
        _programSubscription.Dispose();
    }
}
