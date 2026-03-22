using System.Collections.ObjectModel;
using System.Windows.Input;
using CommunityToolkit.Mvvm.Input;
using Avalonia.Threading;

namespace TV_Player.AvaloniaApp.ViewModels;

public class ProgramsListViewModel : TV_Player.ObservableViewModelBase, IDisposable
{
    private readonly IDisposable _programSubscription;
    private ObservableCollection<M3UInfo> _programs = new();

    public ObservableCollection<M3UInfo> Programs
    {
        get => _programs;
        set => SetProperty(ref _programs, value);
    }

    public ICommand SelectProgramCommand { get; }

    public ProgramsListViewModel(GroupInfo groupInfo)
    {
        SelectProgramCommand = new RelayCommand<M3UInfo>(OnItemSelected);
        _programSubscription = TVPlayerViewModel.Instance.CurrentProgramsData!
            .AllPrograms
            .Subscribe(newPrograms =>
            {
                var filteredPrograms = newPrograms
                    .Where(p => p.GroupTitle == groupInfo.Name && !string.IsNullOrWhiteSpace(p.Url))
                    .ToList();

                Dispatcher.UIThread.Post(() =>
                {
                    Programs = new ObservableCollection<M3UInfo>(filteredPrograms);
                });
            });
    }

    private void OnItemSelected(M3UInfo? program)
    {
        if (program == null)
            return;

        TVPlayerViewModel.Instance.ShowPlayerScreen(program);
    }

    public void Dispose()
    {
        _programSubscription.Dispose();
    }
}
