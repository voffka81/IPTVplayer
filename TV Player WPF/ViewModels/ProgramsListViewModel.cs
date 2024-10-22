using CommunityToolkit.Mvvm.Input;
using System.Windows.Input;
using TV_Player.ViewModels;

namespace TV_Player
{
    public class ProgramsListViewModel : ObservableViewModelBase, IDisposable
    {
        private List<M3UInfo> _programs;
        public List<M3UInfo> Programs
        {
            get => _programs;
            set => SetProperty(ref _programs, value);
        }

        public M3UInfo SelectedItem { get; set; }
        public ICommand ItemSelectedCommand { get; }
        private IDisposable _programSubscriber;

        public ProgramsListViewModel(string playlistName,GroupInfo groupInfo)
        {
            TVPlayerViewModel.Instance.TopPanelVisible(true, groupInfo.Name);
            ItemSelectedCommand = new RelayCommand(OnItemSelected);
            _programSubscriber = TVPlayerViewModel.Instance.CurrentProgrmsData.AllPrograms.Subscribe(x => Programs = x.Where(p => p.GroupTitle == groupInfo.Name).ToList());

            TVPlayerViewModel.Instance.SetBackButtonAction(new Action(() =>
            {
                TVPlayerViewModel.Instance.ShowProgramsGroupScreen(playlistName);
            }));
        }

        private void OnItemSelected()
        {
            TVPlayerViewModel.Instance.ShowPlayerScreen(SelectedItem);
        }

        public void Dispose()
        {
            _programSubscriber.Dispose();
        }
    }
}
