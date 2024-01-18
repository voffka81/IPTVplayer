using CommunityToolkit.Mvvm.Input;
using System.Windows.Input;
using TV_Player.ViewModels;

namespace TV_Player
{
    public class ProgramsListViewModel : ObservableViewModelBase
    {
        private List<M3UInfo> _programs;
        public List<M3UInfo> Programs
        {
            get => _programs;
            set => SetProperty(ref _programs, value);
        }

        public M3UInfo SelectedItem { get; set; }
        public ICommand ItemSelectedCommand { get; }

        public ProgramsListViewModel(GroupInfo groupInfo)
        {
            ItemSelectedCommand = new RelayCommand(OnItemSelected);
            ProgramsData.Instance.AllPrograms.Subscribe(x=>Programs = x.Where(p=>p.GroupTitle== groupInfo.Name).ToList());
        }

        private void OnItemSelected()
        {

            var playerViewModel = new PlayerViewModel(SelectedItem);
            var conrtrol = new VideoPlayer();
            conrtrol.SourceUrl = SelectedItem.Url;
            TVPlayerViewModel.Instance.SetPageContext(conrtrol, playerViewModel);
            //var navigation = (INavigation)Application.Current.MainPage.Navigation;

            //var playerViewModel = new PlayerViewModel(SelectedItem);

            //// Create a new SecondPage and set its BindingContext to the ViewModel
            //var playerPage = new PlayerPage
            //{
            //    BindingContext = playerViewModel
            //};
            //// Navigate to the OtherPage
            //navigation.PushAsync(playerPage);
        }
    }
}
