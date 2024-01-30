using System.Windows.Controls;

namespace TV_Player.ViewModels
{
    public class TVPlayerViewModel : IDisposable
    {
        private readonly MainViewModel _mainViewModel;
        public ProgramsData PlaylistData { get; private set; }

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
            PlaylistData = new ProgramsData();

            _mainViewModel = new MainViewModel();
            var mainWindow = new MainWindow();
            mainWindow.DataContext = _mainViewModel;

            mainWindow.Show();
            _instance = this;

            SettingsModel.LoadSettings();
            InitializeTVWithData();
        }

        public void InitializeTVWithData()
        {
            if (!string.IsNullOrEmpty(SettingsModel.PlaylistURL))
            {
                if (SettingsModel.StartFullScreen)
                    FullScreenToggle();
                PlaylistData.GetData(SettingsModel.PlaylistURL);
                if (SettingsModel.StartFromLastScreen)
                    SelectScreen();
                else
                    ShowProgramsGroupScreen();
            }
            else
            {
                ShowSettingsScreen();
            }
        }

        public void SelectScreen()
        {
            switch (SettingsModel.LastScreen)
            {
                case "ProgramsListViewModel":
                    ShowProgramsListScreen(SettingsModel.Group);
                    break;
                case "PlayerViewModel":
                    ShowPlayerScreen(SettingsModel.Program);
                    break;
                default:
                    ShowProgramsGroupScreen();
                    break;
            }
        }

        public void ShowProgramsGroupScreen()
        {
            var vm = new ProgramsGroupViewModel();

            var control = new ProgramsGroupGrid();
            control.DataContext = vm;

            SettingsModel.LastScreen = nameof(ProgramsGroupViewModel);
            SetPageContext(control, vm);
        }

        public void ShowProgramsListScreen(GroupInfo group)
        {
            SettingsModel.Group = group;
            var programListViewModel = new ProgramsListViewModel(group);
            var conrtrol = new ProgramsList();
            SettingsModel.LastScreen = nameof(ProgramsListViewModel);
            SetPageContext(conrtrol, programListViewModel);
        }

        public void ShowPlayerScreen(M3UInfo program)
        {
            SettingsModel.Program = program;
            var playerViewModel = new PlayerViewModel(program);
            var conrtrol = new VideoPlayer();
            conrtrol.SourceUrl = program.Url;
            SettingsModel.LastScreen = nameof(PlayerViewModel);
            SetPageContext(conrtrol, playerViewModel);
        }

        public void ShowSettingsScreen()
        {
            var playerViewModel = new SettingsViewModel();
            var conrtrol = new Settings();
            SetPageContext(conrtrol, playerViewModel);
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

        private void SetPageContext(ContentControl control, object viewModel)
        {
            if (_mainViewModel.Control is IDisposable disposable)
                disposable.Dispose();
            control.DataContext = viewModel;

            _mainViewModel.Control = control;

            SettingsModel.SaveSetttings();
        }

        public void Dispose()
        {
            if (_mainViewModel.Control is IDisposable disposable)
                disposable.Dispose();
        }
    }
}
