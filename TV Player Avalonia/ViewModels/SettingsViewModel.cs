using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;
using System.Windows.Input;
using TV_Player.ViewModels;

namespace TV_Player.AvaloniaApp.ViewModels;

public class SettingsViewModel : TV_Player.ObservableViewModelBase
{
    private string _playlistName = string.Empty;
    public string PlaylistName
    {
        get => _playlistName;
        set => SetProperty(ref _playlistName, value);
    }

    private string _playlistUrl = string.Empty;
    public string PlaylistURL
    {
        get => _playlistUrl;
        set => SetProperty(ref _playlistUrl, value);
    }

    private ObservableCollection<KeyValuePair<string, string>> _playlists = new();
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
        SaveCommand = new RelayCommand(OnSaveSettings);
        BackCommand = new RelayCommand(OnBackCommand);
        AddPlaylistCommand = new RelayCommand(OnAddPlaylistCommand);
        PlaylistDeleteCommand = new RelayCommand<KeyValuePair<string, string>>(OnPlaylistDeleteCommand);

        StartFullScreen = SettingsModel.StartFullScreen;
        StartLastScreen = SettingsModel.StartFromLastScreen;
        Playlists = SettingsModel.Playlists == null
            ? new ObservableCollection<KeyValuePair<string, string>>()
            : new ObservableCollection<KeyValuePair<string, string>>(SettingsModel.Playlists);
    }

    private void OnAddPlaylistCommand()
    {
        var url = PlaylistURL?.Trim();
        var name = PlaylistName?.Trim();
        if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(url))
            return;

        if (!Uri.TryCreate(url, UriKind.Absolute, out var uriResult) ||
            (uriResult.Scheme != Uri.UriSchemeHttp && uriResult.Scheme != Uri.UriSchemeHttps))
            return;

        if (Playlists.Any(pair => pair.Key.Equals(name, StringComparison.OrdinalIgnoreCase)))
            return;

        Playlists.Add(new KeyValuePair<string, string>(name, url));
        PlaylistName = string.Empty;
        PlaylistURL = string.Empty;
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
        SettingsModel.Playlists = Playlists.ToDictionary(pair => pair.Key, pair => pair.Value);
        SettingsModel.SaveSetttings();
        TVPlayerViewModel.Instance.InitializeTVWithData();
    }
}
