using System.Windows.Input;

namespace TV_Player
{
    public class MainViewModel : ObservableViewModelBase
    {
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
            ProgramsData.Instance.GroupsInformation.Subscribe(x=>Programs = x);
        }

        private void OnItemSelected()
        {
           
        }
    }
}
