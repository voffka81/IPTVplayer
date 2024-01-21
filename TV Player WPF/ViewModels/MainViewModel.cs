using CommunityToolkit.Mvvm.Input;
using System.Windows.Controls;
using System.Windows.Input;

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

        private bool _isTopPanelVisible;
        public bool IsTopPanelVisible{
            get => _isTopPanelVisible;
            set => SetProperty(ref _isTopPanelVisible, value);
        }

        public Action ButtonBackAction { get; set; }
        public ICommand BackCommand { get; }

        public MainViewModel()
        {
            var vm = new ProgramsGroupViewModel();

            var control = new ProgramsGroupGrid();
            control.DataContext = vm;
            Control = control;

            BackCommand = new RelayCommand(OnButtonBackClick);
        }
        private void OnButtonBackClick()
        {
            ButtonBackAction?.Invoke();
        }
    }
}
