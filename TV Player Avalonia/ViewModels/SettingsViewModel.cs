using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;
using System.Windows.Input;
using TV_Player.Plugins;
using TV_Player.ViewModels;

namespace TV_Player.AvaloniaApp.ViewModels;

public class SettingsViewModel : TV_Player.ObservableViewModelBase
{
    public IReadOnlyList<string> LanguageNames => TV_Player.Localization.I18n.LanguageNames;
    private int _languageIndex = SettingsModel.Language == "ru" ? 0 : 1;
    public int LanguageIndex
    {
        get => _languageIndex;
        set => SetProperty(ref _languageIndex, value);
    }
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

    private ObservableCollection<PlaylistEntry> _playlists = new();
    public ObservableCollection<PlaylistEntry> Playlists
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
    public ObservableCollection<ProviderPluginToggleEntry> ProviderPluginEntries { get; }
    public ObservableCollection<PluginSettingEditor> PluginSettingEditors { get; } = new();
    public ObservableCollection<ProviderSourceConfiguration> PluginSources { get; }
    public bool IsNoProviderSources => PluginSources.Count == 0;
    public string PluginDirectory { get; }
    public string PluginLogPath { get; }
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

    private string _pluginSourceMessage = string.Empty;
    public string PluginSourceMessage
    {
        get => _pluginSourceMessage;
        set => SetProperty(ref _pluginSourceMessage, value);
    }

    private string _playlistError = string.Empty;
    public string PlaylistError
    {
        get => _playlistError;
        set => SetProperty(ref _playlistError, value);
    }

    private string _settingsSaveMessage = string.Empty;
    public string SettingsSaveMessage
    {
        get => _settingsSaveMessage;
        set => SetProperty(ref _settingsSaveMessage, value);
    }

    public ICommand SaveCommand { get; }
    public ICommand PlaylistDeleteCommand { get; }
    public ICommand PlaylistMoveUpCommand { get; }
    public ICommand PlaylistMoveDownCommand { get; }
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
        PlaylistDeleteCommand = new RelayCommand<PlaylistEntry>(OnPlaylistDeleteCommand);
        PlaylistMoveUpCommand = new RelayCommand<PlaylistEntry>(entry => MovePlaylist(entry, -1));
        PlaylistMoveDownCommand = new RelayCommand<PlaylistEntry>(entry => MovePlaylist(entry, 1));
        AddPluginSourceCommand = new RelayCommand(OnAddPluginSourceCommand);
        PluginSourceDeleteCommand = new RelayCommand<ProviderSourceConfiguration>(OnPluginSourceDeleteCommand);
        ShowSourcesSectionCommand = new RelayCommand(() => SelectedSettingsSection = "sources");
        ShowPluginsSectionCommand = new RelayCommand(() => SelectedSettingsSection = "plugins");
        ShowPlaybackSectionCommand = new RelayCommand(() => SelectedSettingsSection = "playback");

        StartFullScreen = SettingsModel.StartFullScreen;
        StartLastScreen = SettingsModel.StartFromLastScreen;
        var savedPlaylists = SettingsModel.Playlists ?? new Dictionary<string, string>();
        var playlistOrder = SettingsModel.PlaylistOrder ?? new List<string>();
        var orderedNames = playlistOrder
            .Where(savedPlaylists.ContainsKey)
            .Distinct(StringComparer.Ordinal)
            .Concat(savedPlaylists.Keys.Where(name => !playlistOrder.Contains(name, StringComparer.Ordinal)))
            .ToList();
        Playlists = new ObservableCollection<PlaylistEntry>(orderedNames.Select(name =>
            new PlaylistEntry(name, savedPlaylists[name])));

        TVPlayerViewModel.Instance.LoadProviderPlugins();
        PluginDirectory = TVPlayerViewModel.Instance.PluginDirectory;
        PluginLogPath = TVPlayerViewModel.Instance.PluginLogPath;
        DevelopmentPluginDirectory = TVPlayerViewModel.Instance.DevelopmentPluginDirectory;
        AvailableProviderPlugins = new ObservableCollection<IChannelProviderPlugin>(
            TVPlayerViewModel.Instance.ProviderRegistry.Plugins.OrderBy(plugin => plugin.Name));
        PluginLoadErrors = string.Join(Environment.NewLine, TVPlayerViewModel.Instance.ProviderRegistry.LoadErrors);
        PluginAvailabilityMessage = AvailableProviderPlugins.Count == 0
            ? TV_Player.Localization.I18n.Get("Ta42ea273052f")
            : TV_Player.Localization.I18n.Get("Td45c31b7ee6f");
        PluginSources = new ObservableCollection<ProviderSourceConfiguration>(
            (SettingsModel.ProviderSources ?? new List<ProviderSourceConfiguration>()).Select(CloneProviderSource));
        PluginSources.CollectionChanged += OnPluginSourcesChanged;
        ProviderPluginEntries = new ObservableCollection<ProviderPluginToggleEntry>(
            AvailableProviderPlugins.Select(plugin => new ProviderPluginToggleEntry(plugin)));
        foreach (var entry in ProviderPluginEntries)
        {
            UpdatePluginToggleState(entry, notify: false);
            entry.EnabledChanged += OnProviderPluginEnabledChanged;
        }
        SelectedProviderPlugin = AvailableProviderPlugins.FirstOrDefault();
    }

    private static ProviderSourceConfiguration CloneProviderSource(ProviderSourceConfiguration source) => new()
    {
        Id = source.Id,
        Name = source.Name,
        PluginId = source.PluginId,
        Enabled = source.Enabled,
        Settings = new Dictionary<string, string>(source.Settings ?? new Dictionary<string, string>(), StringComparer.OrdinalIgnoreCase)
    };

    private void OnPluginSourcesChanged(object? sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
    {
        RaisePropertyChanged(nameof(IsNoProviderSources));
        foreach (var entry in ProviderPluginEntries)
            UpdatePluginToggleState(entry, notify: true);
    }

    private void UpdatePluginToggleState(ProviderPluginToggleEntry entry, bool notify)
    {
        var sources = PluginSources.Where(source => string.Equals(source.PluginId, entry.Plugin.Id, StringComparison.OrdinalIgnoreCase)).ToList();
        entry.SetHasConfiguredSource(sources.Count > 0, notify);
        entry.SetEnabled(sources.Any(source => source.Enabled), notify);
    }

    private void OnProviderPluginEnabledChanged(ProviderPluginToggleEntry entry)
    {
        PluginSourceError = string.Empty;
        PluginSourceMessage = string.Empty;
        var matchingSources = PluginSources
            .Where(source => string.Equals(source.PluginId, entry.Plugin.Id, StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (!entry.IsEnabled)
        {
            foreach (var source in matchingSources)
                source.Enabled = false;
            PluginSourceMessage = TV_Player.Localization.I18n.Format("T5a5a3d044b37", entry.Name);
            return;
        }

        if (matchingSources.Count > 0)
        {
            foreach (var source in matchingSources)
                source.Enabled = true;
            PluginSourceMessage = TV_Player.Localization.I18n.Format("T352d5b117b44", entry.Name);
            return;
        }

        IReadOnlyList<PluginSettingDefinition> definitions;
        try
        {
            definitions = entry.Plugin.Settings;
        }
        catch (Exception ex)
        {
            entry.SetEnabled(false, notify: false);
            PluginSourceError = TV_Player.Localization.I18n.Format("T5f815b0d3173", entry.Name, ex.Message);
            return;
        }

        var missingSetting = definitions.FirstOrDefault(setting =>
            setting.IsRequired && string.IsNullOrWhiteSpace(setting.DefaultValue));
        if (missingSetting != null)
        {
            entry.SetEnabled(false, notify: false);
            PluginSourceError = TV_Player.Localization.I18n.Format("T840cb7323886", entry.Name, missingSetting.Label);
            return;
        }

        var sourceName = entry.Plugin.Name.Trim();
        if (Playlists.Any(playlist => playlist.Name.Equals(sourceName, StringComparison.OrdinalIgnoreCase)) ||
            PluginSources.Any(source => source.Name.Equals(sourceName, StringComparison.OrdinalIgnoreCase)))
        {
            entry.SetEnabled(false, notify: false);
            PluginSourceError = TV_Player.Localization.I18n.Format("T43a15549e794", sourceName);
            return;
        }

        var configuration = new ProviderSourceConfiguration
        {
            Id = $"{entry.Plugin.Id}:{Guid.NewGuid():N}",
            Name = sourceName,
            PluginId = entry.Plugin.Id,
            Enabled = true,
            Settings = definitions.ToDictionary(setting => setting.Key, setting => setting.DefaultValue ?? string.Empty,
                StringComparer.OrdinalIgnoreCase)
        };
        PluginSources.Add(configuration);
        PluginSourceMessage = TV_Player.Localization.I18n.Format("Td2052dfa7f64", sourceName);
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
            PluginSourceError = TV_Player.Localization.I18n.Format("Tfc44bb9959bb", ex.Message);
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

        if (Playlists.Any(pair => pair.Name.Equals(name, StringComparison.OrdinalIgnoreCase)) ||
            PluginSources.Any(source => source.Name.Equals(name, StringComparison.OrdinalIgnoreCase)))
            return;

        Playlists.Add(new PlaylistEntry(name, url));
        PlaylistName = string.Empty;
        PlaylistURL = string.Empty;
    }

    private void OnPlaylistDeleteCommand(PlaylistEntry? pair)
    {
        if (pair != null)
            Playlists.Remove(pair);
    }

    private void MovePlaylist(PlaylistEntry? entry, int offset)
    {
        if (entry == null)
            return;

        var index = Playlists.IndexOf(entry);
        var targetIndex = index + offset;
        if (index >= 0 && targetIndex >= 0 && targetIndex < Playlists.Count)
            Playlists.Move(index, targetIndex);
    }

    private void OnAddPluginSourceCommand()
    {
        PluginSourceError = string.Empty;
        PluginSourceMessage = string.Empty;
        var name = NewPluginSourceName.Trim();
        var plugin = SelectedProviderPlugin;
        if (plugin == null)
        {
            PluginSourceError = TV_Player.Localization.I18n.Get("T6a36f87c7468");
            return;
        }

        if (string.IsNullOrWhiteSpace(name))
        {
            PluginSourceError = TV_Player.Localization.I18n.Get("T68432be97cc3");
            return;
        }

        if (Playlists.Any(pair => pair.Name.Equals(name, StringComparison.OrdinalIgnoreCase)) ||
            PluginSources.Any(source => source.Name.Equals(name, StringComparison.OrdinalIgnoreCase)))
        {
            PluginSourceError = TV_Player.Localization.I18n.Get("T0601c14b0070");
            return;
        }

        var missingSetting = PluginSettingEditors.FirstOrDefault(setting =>
            setting.IsRequired && string.IsNullOrWhiteSpace(setting.Value));
        if (missingSetting != null)
        {
            PluginSourceError = TV_Player.Localization.I18n.Format("T16234b3ed850", missingSetting.Label);
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
        PluginSourceMessage = TV_Player.Localization.I18n.Format("T6ea6c44622b9", name);
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
        SettingsSaveMessage = string.Empty;
        PlaylistError = string.Empty;
        PluginSourceError = string.Empty;
        var names = Playlists.Select(entry => entry.Name.Trim()).ToList();
        var canSavePlaylists = true;
        if (names.Any(string.IsNullOrWhiteSpace))
        {
            PlaylistError = TV_Player.Localization.I18n.Get("Tb0f1152ec2ee");
            canSavePlaylists = false;
        }
        else if (names.Distinct(StringComparer.OrdinalIgnoreCase).Count() != names.Count ||
                 names.Any(name => PluginSources.Any(source => source.Name.Equals(name, StringComparison.OrdinalIgnoreCase))))
        {
            PlaylistError = TV_Player.Localization.I18n.Get("Tac666cbe36e8");
            canSavePlaylists = false;
        }
        else if (Playlists.Any(entry => !Uri.TryCreate(entry.Url.Trim(), UriKind.Absolute, out var uri) ||
                                        (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps)))
        {
            PlaylistError = TV_Player.Localization.I18n.Get("T799d82ceefea");
            canSavePlaylists = false;
        }

        var providerNames = PluginSources.Select(source => source.Name?.Trim() ?? string.Empty).ToList();
        var canSaveProviderSources = !providerNames.Any(string.IsNullOrWhiteSpace) &&
            providerNames.Distinct(StringComparer.OrdinalIgnoreCase).Count() == providerNames.Count &&
            !providerNames.Any(name => names.Any(playlistName =>
                string.Equals(name, playlistName, StringComparison.OrdinalIgnoreCase)));
        if (!canSaveProviderSources)
            PluginSourceError = TV_Player.Localization.I18n.Get("Tf7181f174827");

        // VLC is the only engine connected to the Avalonia video surface.
        SettingsModel.PlaybackEngine = "vlc";
        if (canSavePlaylists && canSaveProviderSources)
        {
            SettingsModel.Language = LanguageIndex == 0 ? "ru" : "en";
            TV_Player.Localization.I18n.SetLanguage(SettingsModel.Language);
        }
        SettingsModel.StartFullScreen = StartFullScreen;
        SettingsModel.StartFromLastScreen = StartLastScreen;
        if (canSavePlaylists)
        {
            foreach (var entry in Playlists)
                entry.Name = entry.Name.Trim();
            SettingsModel.Playlists = Playlists.ToDictionary(entry => entry.Name, entry => entry.Url.Trim());
            SettingsModel.PlaylistOrder = Playlists.Select(entry => entry.Name).ToList();
        }
        if (canSaveProviderSources)
        {
            foreach (var source in PluginSources)
                source.Name = source.Name.Trim();
            SettingsModel.ProviderSources = PluginSources.ToList();
        }
        SettingsModel.SaveSetttings();
        TVPlayerViewModel.Instance.InitializeTVWithData();
        SettingsSaveMessage = canSavePlaylists && canSaveProviderSources
            ? TV_Player.Localization.I18n.Get("Tae493e21cd43")
            : TV_Player.Localization.I18n.Get("T1cd12062a7cb");
        if (canSavePlaylists && canSaveProviderSources)
            OnBackCommand();
    }
}

public sealed class PlaylistEntry : TV_Player.ObservableViewModelBase
{
    private string _name;
    private string _url;

    public string Name
    {
        get => _name;
        set => SetProperty(ref _name, value);
    }

    public string Url
    {
        get => _url;
        set => SetProperty(ref _url, value);
    }

    public PlaylistEntry(string name, string url)
    {
        _name = name;
        _url = url;
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
        Label = TV_Player.Localization.I18n.TranslateKnown(definition.Label);
        Description = definition.Description == null ? null : TV_Player.Localization.I18n.TranslateKnown(definition.Description);
        IsRequired = definition.IsRequired;
        _value = definition.DefaultValue ?? string.Empty;
    }
}

public sealed class ProviderPluginToggleEntry : TV_Player.ObservableViewModelBase
{
    private bool _isEnabled;
    private bool _hasConfiguredSource;

    public IChannelProviderPlugin Plugin { get; }
    public string Name => Plugin.Name;
    public event Action<ProviderPluginToggleEntry>? EnabledChanged;

    public bool CanEnable => _hasConfiguredSource || HasUsableDefaults();

    public string StatusText => _hasConfiguredSource
        ? (_isEnabled ? TV_Player.Localization.I18n.Get("Te92cc38813f9") : TV_Player.Localization.I18n.Get("T82f628545854"))
        : CanEnable ? TV_Player.Localization.I18n.Get("T851bf3886cc2") : TV_Player.Localization.I18n.Get("T937096daecb5");

    public bool IsEnabled
    {
        get => _isEnabled;
        set
        {
            if (!SetProperty(ref _isEnabled, value))
                return;

            RaisePropertyChanged(nameof(StatusText));
            EnabledChanged?.Invoke(this);
        }
    }

    public ProviderPluginToggleEntry(IChannelProviderPlugin plugin) =>
        Plugin = plugin ?? throw new ArgumentNullException(nameof(plugin));

    public void SetEnabled(bool value, bool notify)
    {
        if (notify)
        {
            IsEnabled = value;
            return;
        }

        if (SetProperty(ref _isEnabled, value, nameof(IsEnabled)))
            RaisePropertyChanged(nameof(StatusText));
    }

    public void SetHasConfiguredSource(bool value, bool notify)
    {
        if (_hasConfiguredSource == value)
            return;

        _hasConfiguredSource = value;
        RaisePropertyChanged(nameof(CanEnable));
        RaisePropertyChanged(nameof(StatusText));
    }

    private bool HasUsableDefaults()
    {
        try
        {
            return Plugin.Settings.All(setting =>
                !setting.IsRequired || !string.IsNullOrWhiteSpace(setting.DefaultValue));
        }
        catch
        {
            return false;
        }
    }
}
