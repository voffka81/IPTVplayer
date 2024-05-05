using System.Windows.Input;

namespace TV_Player.MAUI
{
    public class MainViewModel : ObservableViewModelBase
    {
        private IDisposable _groupsSubscriber;
        private List<GroupInfo> _programs;
        public List<GroupInfo> Programs
        {
            get => _programs;
            set => SetProperty(ref _programs, value);
        }

        public GroupInfo SelectedItem { get; set; }
        public ICommand ItemSelectedCommand { get; }

        public MainViewModel()
        {
            ItemSelectedCommand = new Command(OnItemSelected);
            _groupsSubscriber = TVPlayerViewModel.Instance.PlaylistData.GroupsInformation.Subscribe(x => Programs = x);
        }

        private void OnItemSelected()
        {
            var navigation = (INavigation)Application.Current.MainPage.Navigation;

            var programPageViewModel = new ProgramViewModel(SelectedItem);

            // Create a new SecondPage and set its BindingContext to the ViewModel
            var programPage = new ProgramPage
            {
                BindingContext = programPageViewModel
            };
            // Navigate to the OtherPage
            navigation.PushAsync(programPage);

            _groupsSubscriber.Dispose();
        }
    }
}
