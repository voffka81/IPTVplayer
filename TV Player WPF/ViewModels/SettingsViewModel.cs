using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;
using System.Windows.Input;

namespace TV_Player.ViewModels
{
    internal class SettingsViewModel : ObservableViewModelBase
    {
        private string _playlistName;
        public string PlaylistName
        {
            get => _playlistName;
            set => SetProperty(ref _playlistName, value);
        }

        private string _playlistURL;
        public string PlaylistURL
        {
            get => _playlistURL;
            set => SetProperty(ref _playlistURL, value);
        }

        private ObservableCollection<KeyValuePair<string, string>> _playlists;
        public ObservableCollection<KeyValuePair<string, string>> Playlists
        {
            get => _playlists;
            set => SetProperty(ref _playlists, value);
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
        public ICommand PlaylistDeleteCommand { get; }
        public ICommand BackCommand { get; }
        public ICommand AddPlaylistCommand { get; }

        public SettingsViewModel()
        {
            TVPlayerViewModel.Instance.TopPanelVisible(false, "");

            SaveCommand = new RelayCommand(OnSaveSettings);
            BackCommand = new RelayCommand(OnBackCommand);
            AddPlaylistCommand = new RelayCommand(OnAddPlaylistCommand);
            PlaylistDeleteCommand = new RelayCommand<KeyValuePair<string, string>>(OnPlaylistDeleteCommand);

            StartFullScreen = SettingsModel.StartFullScreen;
            StartLastScreen = SettingsModel.StartFromLastScreen;
            Playlists = new ObservableCollection<KeyValuePair<string, string>>(SettingsModel.Playlists);
        }

        private void OnAddPlaylistCommand()
        {
            Playlists.Add(new KeyValuePair<string, string>(PlaylistName, PlaylistURL));
        }

        private void OnPlaylistDeleteCommand(KeyValuePair<string, string> pair)
        {
            Playlists.Remove(pair);
        }


        private void OnBackCommand()
        {
            TVPlayerViewModel.Instance.SelectScreen();
        }

        private void OnSaveSettings()
        {
            SettingsModel.StartFullScreen = StartFullScreen;
            SettingsModel.StartFromLastScreen = StartLastScreen;
            SettingsModel.Playlists.Clear();
            SettingsModel.Playlists = Playlists.ToDictionary<string, string>();
            SettingsModel.SaveSetttings();
            TVPlayerViewModel.Instance.InitializeTVWithData();
        }
    }
}