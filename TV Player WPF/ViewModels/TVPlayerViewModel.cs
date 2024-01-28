using System.Windows.Controls;

namespace TV_Player.ViewModels
{
    public class TVPlayerViewModel : IDisposable
    {
        private readonly MainViewModel _mainViewModel;

        public Action ButtonBackAction { get; set; }

        private static TVPlayerViewModel? _instance;
        public static TVPlayerViewModel Instance
        {
            get
            {
                if (_instance == null)
                    _instance = new TVPlayerViewModel();
                return _instance;
            }
        }

        public TVPlayerViewModel()
        {
            _mainViewModel = new MainViewModel();
            var mainWindow = new MainWindow();
            mainWindow.DataContext = _mainViewModel;

            mainWindow.Show();
            _instance = this;

            ShowInitialScreen();
        }

        private void ShowInitialScreen()
        {
            var vm = new ProgramsGroupViewModel();

            var control = new ProgramsGroupGrid();
            control.DataContext = vm;

            SetPageContext(control, vm);
        }

        public void TopPanelVisible(bool value, string title)
        {
            _mainViewModel.IsTopPanelVisible = value;
            _mainViewModel.TopPanelTitle = title;
        }

        public void FullScreenToggle()
        {
            _mainViewModel.OnFullSctreenButtonClick();
        }

        public void SetBackButtonAction(Action action)
        {
            _mainViewModel.ButtonBackAction = action;
        }

        public void SetPageContext(ContentControl control, object viewModel)
        {
            if (_mainViewModel.Control is IDisposable disposable)
                disposable.Dispose();
            control.DataContext = viewModel;

            _mainViewModel.Control = control;
        }

        public void Dispose()
        {
            if (_mainViewModel.Control is IDisposable disposable)
                disposable.Dispose();
        }
    }
}
