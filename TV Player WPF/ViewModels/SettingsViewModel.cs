using CommunityToolkit.Mvvm.Input;
using System.Windows.Input;

namespace TV_Player.ViewModels
{
    internal class SettingsViewModel : ObservableViewModelBase
    {
        private string _playlistURL;
        public string PlaylistURL
        {
            get => _playlistURL;
            set => SetProperty(ref _playlistURL, value);
        }

        private bool _startFullScreen;
        public bool StartFullScreen
        {
            get => _startFullScreen;
            set => SetProperty(ref _startFullScreen, value);
        }

        private bool _startLastScreen;
        public bool StartLastScreen
        {
            get => _startLastScreen;
            set => SetProperty(ref _startLastScreen, value);
        }

        public ICommand SaveCommand { get; }
        public ICommand BackCommand { get; }

        public SettingsViewModel()
        {
            TVPlayerViewModel.Instance.TopPanelVisible(false, "");

            SaveCommand = new RelayCommand(OnSaveSettings);
            BackCommand = new RelayCommand(OnBackCommand);

            StartFullScreen = SettingsModel.StartFullScreen;
            StartLastScreen = SettingsModel.StartFromLastScreen;
            PlaylistURL = SettingsModel.PlaylistURL;
        }

        private void OnBackCommand()
        {
            TVPlayerViewModel.Instance.SelectScreen();
        }

        private void OnSaveSettings()
        {
            SettingsModel.StartFullScreen = StartFullScreen;
            SettingsModel.StartFromLastScreen = StartLastScreen;
            SettingsModel.PlaylistURL = PlaylistURL;

            SettingsModel.SaveSetttings();
            TVPlayerViewModel.Instance.InitializeTVWithData(); 
        }
    }
}