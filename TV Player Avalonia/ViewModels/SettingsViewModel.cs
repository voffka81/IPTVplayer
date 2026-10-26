using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;
using System.Windows.Input;
using TV_Player.Plugins;
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

    public string[] PlaybackEngines { get; } = ["vlc", "mpv"];

    private string _selectedPlaybackEngine = "vlc";
    public string SelectedPlaybackEngine
    {
        get => _selectedPlaybackEngine;
        set => SetProperty(ref _selectedPlaybackEngine, value);
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

    private string _selectedSettingsSection = "sources";
    public string SelectedSettingsSection
    {
        get => _selectedSettingsSection;
        private set
        {
            if (SetProperty(ref _selectedSettingsSection, value))
            {
                RaisePropertyChanged(nameof(IsSourcesSection));
                RaisePropertyChanged(nameof(IsPluginsSection));
                RaisePropertyChanged(nameof(IsPlaybackSection));
            }
        }
    }

    public bool IsSourcesSection => SelectedSettingsSection == "sources";
    public bool IsPluginsSection => SelectedSettingsSection == "plugins";
    public bool IsPlaybackSection => SelectedSettingsSection == "playback";

    public ObservableCollection<IChannelProviderPlugin> AvailableProviderPlugins { get; }
    public ObservableCollection<PluginSettingEditor> PluginSettingEditors { get; } = new();
    public ObservableCollection<ProviderSourceConfiguration> PluginSources { get; }
    public string PluginDirectory { get; }
    public string DevelopmentPluginDirectory { get; }
    public string PluginLoadErrors { get; }
    public string PluginAvailabilityMessage { get; }

    private IChannelProviderPlugin? _selectedProviderPlugin;
    public IChannelProviderPlugin? SelectedProviderPlugin
    {
        get => _selectedProviderPlugin;
        set
        {
            if (SetProperty(ref _selectedProviderPlugin, value))
                RefreshPluginSettingEditors();
        }
    }

    private string _newPluginSourceName = string.Empty;
    public string NewPluginSourceName
    {
        get => _newPluginSourceName;
        set => SetProperty(ref _newPluginSourceName, value);
    }

    private string _pluginSourceError = string.Empty;
    public string PluginSourceError
    {
        get => _pluginSourceError;
        set => SetProperty(ref _pluginSourceError, value);
    }

    public ICommand SaveCommand { get; }
    public ICommand PlaylistDeleteCommand { get; }
    public ICommand BackCommand { get; }
    public ICommand AddPlaylistCommand { get; }
    public ICommand AddPluginSourceCommand { get; }
    public ICommand PluginSourceDeleteCommand { get; }
    public ICommand ShowSourcesSectionCommand { get; }
    public ICommand ShowPluginsSectionCommand { get; }
    public ICommand ShowPlaybackSectionCommand { get; }

    public SettingsViewModel()
    {
        SaveCommand = new RelayCommand(OnSaveSettings);
        BackCommand = new RelayCommand(OnBackCommand);
        AddPlaylistCommand = new RelayCommand(OnAddPlaylistCommand);
        PlaylistDeleteCommand = new RelayCommand<KeyValuePair<string, string>>(OnPlaylistDeleteCommand);
        AddPluginSourceCommand = new RelayCommand(OnAddPluginSourceCommand);
        PluginSourceDeleteCommand = new RelayCommand<ProviderSourceConfiguration>(OnPluginSourceDeleteCommand);
        ShowSourcesSectionCommand = new RelayCommand(() => SelectedSettingsSection = "sources");
        ShowPluginsSectionCommand = new RelayCommand(() => SelectedSettingsSection = "plugins");
        ShowPlaybackSectionCommand = new RelayCommand(() => SelectedSettingsSection = "playback");

        SelectedPlaybackEngine = SettingsModel.PlaybackEngine == "mpv" ? "mpv" : "vlc";
        StartFullScreen = SettingsModel.StartFullScreen;
        StartLastScreen = SettingsModel.StartFromLastScreen;
        Playlists = SettingsModel.Playlists == null
            ? new ObservableCollection<KeyValuePair<string, string>>()
            : new ObservableCollection<KeyValuePair<string, string>>(SettingsModel.Playlists);

        TVPlayerViewModel.Instance.LoadProviderPlugins();
        PluginDirectory = TVPlayerViewModel.Instance.PluginDirectory;
        DevelopmentPluginDirectory = TVPlayerViewModel.Instance.DevelopmentPluginDirectory;
        AvailableProviderPlugins = new ObservableCollection<IChannelProviderPlugin>(
            TVPlayerViewModel.Instance.ProviderRegistry.Plugins.OrderBy(plugin => plugin.Name));
        PluginLoadErrors = string.Join(Environment.NewLine, TVPlayerViewModel.Instance.ProviderRegistry.LoadErrors);
        PluginAvailabilityMessage = AvailableProviderPlugins.Count == 0
            ? "No provider plugins are installed yet."
            : "Select a plugin, then fill in its source settings.";
        PluginSources = new ObservableCollection<ProviderSourceConfiguration>(
            SettingsModel.ProviderSources ?? new List<ProviderSourceConfiguration>());
        SelectedProviderPlugin = AvailableProviderPlugins.FirstOrDefault();
    }

    private void RefreshPluginSettingEditors()
    {
        PluginSettingEditors.Clear();
        PluginSourceError = string.Empty;
        if (SelectedProviderPlugin == null)
            return;

        try
        {
            foreach (var definition in SelectedProviderPlugin.Settings)
                PluginSettingEditors.Add(new PluginSettingEditor(definition));
        }
        catch (Exception ex)
        {
            PluginSourceError = $"Could not read plugin settings: {ex.Message}";
        }
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

        if (Playlists.Any(pair => pair.Key.Equals(name, StringComparison.OrdinalIgnoreCase)) ||
            PluginSources.Any(source => source.Name.Equals(name, StringComparison.OrdinalIgnoreCase)))
            return;

        Playlists.Add(new KeyValuePair<string, string>(name, url));
        PlaylistName = string.Empty;
        PlaylistURL = string.Empty;
    }

    private void OnPlaylistDeleteCommand(KeyValuePair<string, string> pair)
    {
        Playlists.Remove(pair);
    }

    private void OnAddPluginSourceCommand()
    {
        PluginSourceError = string.Empty;
        var name = NewPluginSourceName.Trim();
        var plugin = SelectedProviderPlugin;
        if (plugin == null)
        {
            PluginSourceError = "No provider plugins are installed.";
            return;
        }

        if (string.IsNullOrWhiteSpace(name))
        {
            PluginSourceError = "Enter a name for this source.";
            return;
        }

        if (Playlists.Any(pair => pair.Key.Equals(name, StringComparison.OrdinalIgnoreCase)) ||
            PluginSources.Any(source => source.Name.Equals(name, StringComparison.OrdinalIgnoreCase)))
        {
            PluginSourceError = "A playlist or provider source with this name already exists.";
            return;
        }

        var missingSetting = PluginSettingEditors.FirstOrDefault(setting =>
            setting.IsRequired && string.IsNullOrWhiteSpace(setting.Value));
        if (missingSetting != null)
        {
            PluginSourceError = $"Enter a value for {missingSetting.Label}.";
            return;
        }

        var configuration = new ProviderSourceConfiguration
        {
            Id = $"{plugin.Id}:{Guid.NewGuid():N}",
            Name = name,
            PluginId = plugin.Id,
            Settings = PluginSettingEditors.ToDictionary(setting => setting.Key, setting => setting.Value,
                StringComparer.OrdinalIgnoreCase)
        };

        PluginSources.Add(configuration);
        NewPluginSourceName = string.Empty;
        RefreshPluginSettingEditors();
    }

    private void OnPluginSourceDeleteCommand(ProviderSourceConfiguration? source)
    {
        if (source != null)
            PluginSources.Remove(source);
    }

    private void OnBackCommand()
    {
        TVPlayerViewModel.Instance.SelectScreen();
    }

    private void OnSaveSettings()
    {
        SettingsModel.PlaybackEngine = SelectedPlaybackEngine == "mpv" ? "mpv" : "vlc";
        SettingsModel.StartFullScreen = StartFullScreen;
        SettingsModel.StartFromLastScreen = StartLastScreen;
        SettingsModel.Playlists = Playlists.ToDictionary(pair => pair.Key, pair => pair.Value);
        SettingsModel.ProviderSources = PluginSources.ToList();
        SettingsModel.SaveSetttings();
        TVPlayerViewModel.Instance.InitializeTVWithData();
    }
}

public sealed class PluginSettingEditor : TV_Player.ObservableViewModelBase
{
    private string _value;

    public string Key { get; }
    public string Label { get; }
    public string? Description { get; }
    public bool IsRequired { get; }

    public string Value
    {
        get => _value;
        set => SetProperty(ref _value, value);
    }

    public PluginSettingEditor(PluginSettingDefinition definition)
    {
        Key = definition.Key;
        Label = definition.Label;
        Description = definition.Description;
        IsRequired = definition.IsRequired;
        _value = definition.DefaultValue ?? string.Empty;
    }
}
