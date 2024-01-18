using CommunityToolkit.Mvvm.Input;
using System.Windows.Input;
using TV_Player.ViewModels;

namespace TV_Player
{
    public class ProgramsGroupViewModel : ObservableViewModelBase
    {
        private List<GroupInfo> _programs;
        public List<GroupInfo> Programs
        {
            get => _programs;
            set => SetProperty(ref _programs, value);
        }

        public GroupInfo SelectedItem { get; set; }
        public ICommand ItemSelectedCommand { get; }

        public ProgramsGroupViewModel()
        {
            ItemSelectedCommand = new RelayCommand(OnItemSelected);
            ProgramsData.Instance.GroupsInformation.Subscribe(x=>Programs = x);
        }

        private void OnItemSelected()
        {
            var programListViewModel = new ProgramsListViewModel(SelectedItem);
            var conrtrol = new ProgramsList();
            TVPlayerViewModel.Instance.SetPageContext(conrtrol, programListViewModel);
        }
    }
}
