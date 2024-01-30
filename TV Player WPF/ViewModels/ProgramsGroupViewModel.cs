using CommunityToolkit.Mvvm.Input;
using System.Windows.Input;
using TV_Player.ViewModels;

namespace TV_Player
{
    public class ProgramsGroupViewModel : ObservableViewModelBase, IDisposable
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

        public ProgramsGroupViewModel()
        {
            ItemSelectedCommand = new RelayCommand(OnItemSelected);
            _groupInformationSubscriber = TVPlayerViewModel.Instance.PlaylistData.GroupsInformation.Subscribe(x=>Programs = x);

            TVPlayerViewModel.Instance.TopPanelVisible(true, "Группы");
        }

        private void OnItemSelected()
        {
            TVPlayerViewModel.Instance.ShowProgramsListScreen(SelectedItem);
        }

        public void Dispose()
        {
            _groupInformationSubscriber.Dispose();
        }
    }
}
