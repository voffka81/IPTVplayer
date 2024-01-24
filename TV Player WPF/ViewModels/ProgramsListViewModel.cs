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
        public ProgramsListViewModel(GroupInfo groupInfo)
        {
            TVPlayerViewModel.Instance.TopPanelVisible(true, groupInfo.Name);
            ItemSelectedCommand = new RelayCommand(OnItemSelected);
            _programSubscriber = ProgramsData.Instance.AllPrograms.Subscribe(x => Programs = x.Where(p => p.GroupTitle == groupInfo.Name).ToList());

            TVPlayerViewModel.Instance.SetBackButtonAction(new Action(() =>
            {
                var programGroupViewModel = new ProgramsGroupViewModel();
                var conrtrol = new ProgramsGroupGrid();
                TVPlayerViewModel.Instance.SetPageContext(conrtrol, programGroupViewModel);
            }));
        }

        private void OnItemSelected()
        {
            var playerViewModel = new PlayerViewModel(SelectedItem);
            var conrtrol = new VideoPlayer();
            conrtrol.SourceUrl = SelectedItem.Url;
            TVPlayerViewModel.Instance.SetPageContext(conrtrol, playerViewModel);
        }

        public void Dispose()
        {
            _programSubscriber.Dispose();
        }
    }
}
