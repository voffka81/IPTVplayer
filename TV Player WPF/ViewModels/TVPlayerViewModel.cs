using System.Windows;
using System.Windows.Controls;

namespace TV_Player.ViewModels
{
    public class TVPlayerViewModel : IDisposable
    {
        private readonly MainViewModel _mainViewModel;

        public ProgramsData CurrentProgrmsData { get; private set; }

        public Dictionary<string,ProgramsData> PlayListsData { get; private set; }

        public Action ButtonBackAction { get; set; }

        public string _currentPlaylistName;

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
            PlayListsData=new Dictionary<string,ProgramsData>();

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
            if (SettingsModel.Playlists!=null && SettingsModel.Playlists.Any())
            {
                SetLanguageDictionary();
                PlayListsData.Clear();
                foreach (var playlist in SettingsModel.Playlists)
                {
                    PlayListsData.Add(playlist.Key, new ProgramsData(playlist.Key,playlist.Value));
                }

                if (SettingsModel.StartFullScreen)
                    FullScreenToggle();

                if (SettingsModel.StartFromLastScreen)
                    SelectScreen();
                else
                    ShowPlaylistsGroupScreen();
            }
            else
            {
                ShowSettingsScreen();
            }
        }

        private void SetLanguageDictionary()
        {
            ResourceDictionary dict = new ResourceDictionary();
            switch (Thread.CurrentThread.CurrentCulture.ToString())
            {
                case "en-US":
                    dict.Source = new Uri("..\\Assets\\en-US.xaml", UriKind.Relative);
                    break;
                case "ru-RU":
                    dict.Source = new Uri("..\\Assets\\ru-RU.xaml", UriKind.Relative);
                    break;
                default:
                    dict.Source = new Uri("..\\Assets\\en-US.xaml", UriKind.Relative);
                    break;
            }
            Application.Current.Resources.MergedDictionaries.Add(dict);
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
                    ShowPlaylistsGroupScreen();
                    break;
            }
        }

        public void ShowPlaylistsGroupScreen()
        {
            var vm = new PlaylistsGroupViewModel();
            var control = new PlaylistsGroup();
            control.DataContext = vm;

            SettingsModel.LastScreen = nameof(ProgramsGroupViewModel);
            SetPageContext(control, vm);
        }


        public void ShowProgramsGroupScreen(string groupName)
        {
            var selectedData = PlayListsData.First(x => x.Key == groupName);
            _currentPlaylistName = selectedData.Key;
            CurrentProgrmsData = selectedData.Value;

            var vm = new ProgramsGroupViewModel();

            var control = new ProgramsGroupGrid();
            control.DataContext = vm;

            SettingsModel.LastScreen = nameof(ProgramsGroupViewModel);
            SetPageContext(control, vm);
        }

        public void ShowProgramsListScreen(GroupInfo group)
        {
            SettingsModel.Group = group;
            var programListViewModel = new ProgramsListViewModel(_currentPlaylistName,group);
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

        public void CloseAppCommand()
        {
            _mainViewModel.OnCloseAppButtonClick();
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
