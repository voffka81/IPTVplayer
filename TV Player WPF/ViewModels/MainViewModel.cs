using System.Windows.Controls;

namespace TV_Player
{
    public class MainViewModel : ObservableViewModelBase
    {
        private ContentControl _control;
        public ContentControl Control
        {
            get => _control;
            set => SetProperty(ref _control, value);
        }

        public MainViewModel()
        {
            var vm = new ProgramsGroupViewModel();

            var control = new ProgramsGroupGrid();
            control.DataContext = vm;
            Control = control;
        }
    }
}
