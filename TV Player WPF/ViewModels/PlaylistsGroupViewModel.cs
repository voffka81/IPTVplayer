using CommunityToolkit.Mvvm.Input;
using System.Windows.Input;
using TV_Player.ViewModels;

namespace TV_Player
{
    public class PlaylistsGroupViewModel : ObservableViewModelBase, IDisposable
    {
        private List<GroupInfo> _programs;

        public List<GroupInfo> Programs
        {
            get => _programs;
            set => SetProperty(ref _programs, value);
        }

        public GroupInfo SelectedItem { get; set; }
        public ICommand ItemSelectedCommand { get; }
        public IDisposable _groupInformationSubscriber;

        public PlaylistsGroupViewModel()
        {
            ItemSelectedCommand = new RelayCommand(OnItemSelected);
            Programs = TVPlayerViewModel.Instance.PlayListsData.Select(x=>new GroupInfo() { Name =x.Key,Count=0}).ToList();

            TVPlayerViewModel.Instance.TopPanelVisible(true, "Группы");
        }

        private void OnItemSelected()
        {
            TVPlayerViewModel.Instance.ShowProgramsGroupScreen(SelectedItem.Name);
        }

        public void Dispose()
        {
            _groupInformationSubscriber.Dispose();
        }
    }
}
