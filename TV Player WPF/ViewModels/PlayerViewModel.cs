using CommunityToolkit.Mvvm.Input;
using System.Windows.Input;
using TV_Player.ViewModels;

namespace TV_Player
{
    
    public class PlayerViewModel: ObservableViewModelBase
    {


        private M3UInfo _currentProgram;
        public M3UInfo SelectedProgram
        {
            get => _currentProgram;
            set => SetProperty(ref _currentProgram, value);
        }

        public M3UInfo SelectedItem { get; set; }
        public ICommand BackCommand { get; }

        public PlayerViewModel(M3UInfo selectedProgram)
        {
            _currentProgram = selectedProgram;
            BackCommand = new RelayCommand(OnButtonBackClick);
           
        }

        private void OnButtonBackClick()
        {
            var groupInfo = new GroupInfo() { Name = _currentProgram.GroupTitle, Count = 0 };

            var programListViewModel = new ProgramsListViewModel(groupInfo);
            var conrtrol = new ProgramsList();
            TVPlayerViewModel.Instance.SetPageContext(conrtrol, programListViewModel);
        }

    }
}
