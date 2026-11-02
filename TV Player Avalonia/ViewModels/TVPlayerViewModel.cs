using TV_Player.ViewModels;
using TV_Player.Plugins;
using TV_Player.Providers;

namespace TV_Player.AvaloniaApp.ViewModels;

public class TVPlayerViewModel : IDisposable
{
    private static readonly Lazy<TVPlayerViewModel> LazyInstance = new(() => new TVPlayerViewModel());
    public static TVPlayerViewModel Instance => LazyInstance.Value;

    private readonly MainWindowViewModel _mainWindowViewModel;

    public MainWindowViewModel MainWindowViewModel => _mainWindowViewModel;
    public ProgramsData? CurrentProgramsData { get; private set; }
    public Dictionary<string, ProgramsData> PlayListsData { get; } = new();
    public ChannelProviderRegistry ProviderRegistry { get; } = new();
    public string PluginDirectory => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "TVPlayer", "Plugins");
    public string PluginLogPath => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "TVPlayer", "Logs", "player.log");
    public string DevelopmentPluginDirectory => Path.Combine(AppContext.BaseDirectory, "Plugins");
    private bool _pluginsLoaded;
    public string? CurrentPlaylistName { get; private set; }

    private TVPlayerViewModel()
    {
        _mainWindowViewModel = new MainWindowViewModel();
        SettingsModel.LoadSettings();
    }

    public void Initialize()
    {
        InitializeTVWithData();
    }

    public void LoadProviderPlugins()
    {
        if (_pluginsLoaded)
            return;

        // Development builds copy the sample plugin and dependencies beside the app,
        // so developers can rebuild and run without manually deploying DLLs.
        ProviderRegistry.LoadPlugins(DevelopmentPluginDirectory, PluginDirectory);
        _pluginsLoaded = true;
    }

    public void InitializeTVWithData()
    {
        LoadProviderPlugins();
        foreach (var existingData in PlayListsData.Values)
        {
            try { existingData.Dispose(); } catch { }
        }
        PlayListsData.Clear();

        if (SettingsModel.Playlists is { Count: > 0 })
        {
            var orderedNames = SettingsModel.PlaylistOrder
                .Where(name => SettingsModel.Playlists.ContainsKey(name))
                .Distinct(StringComparer.Ordinal)
                .Concat(SettingsModel.Playlists.Keys.Where(name => !SettingsModel.PlaylistOrder.Contains(name, StringComparer.Ordinal)));
            foreach (var name in orderedNames)
            {
                PlayListsData[name] = new ProgramsData(name, SettingsModel.Playlists[name]);
            }
        }

        foreach (var source in SettingsModel.ProviderSources ?? new List<ProviderSourceConfiguration>())
        {
            if (!source.Enabled)
                continue;

            try
            {
                if (PlayListsData.ContainsKey(source.Name))
                    throw new InvalidOperationException($"A source named '{source.Name}' already exists.");

                var provider = ProviderRegistry.CreateProvider(source);
                PlayListsData[source.Name] = new ProgramsData(source.Name, provider);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[TVPlayerViewModel] Could not initialize plugin source '{source.Name}': {ex.Message}");
            }
        }

        if (PlayListsData.Count > 0)
        {
            if (SettingsModel.StartFullScreen)
            {
                FullScreenToggle();
            }

            if (SettingsModel.StartFromLastScreen)
            {
                SelectScreen();
            }
            else
            {
                ShowPlaylistsGroupScreen();
            }
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
            case nameof(ProgramsListViewModel):
                if (SettingsModel.Group != null)
                    ShowProgramsListScreen(SettingsModel.Group);
                else
                    ShowPlaylistsGroupScreen();
                break;
            case nameof(PlayerViewModel):
                if (SettingsModel.Program != null)
                    ShowPlayerScreen(SettingsModel.Program);
                else
                    ShowPlaylistsGroupScreen();
                break;
            default:
                ShowPlaylistsGroupScreen();
                break;
        }
    }

    public void ShowPlaylistsGroupScreen()
    {
        SettingsModel.LastScreen = nameof(ProgramsGroupViewModel);
        TopPanelVisible(true, "Playlists");
        SetPageContext(new PlaylistsGroupViewModel());
    }

    public void ShowProgramsGroupScreen(string playlistName)
    {
        var selectedData = PlayListsData.First(x => x.Key == playlistName);
        CurrentPlaylistName = selectedData.Key;
        CurrentProgramsData = selectedData.Value;

        SettingsModel.LastScreen = nameof(ProgramsGroupViewModel);
        TopPanelVisible(true, "Groups");
        SetBackButtonAction(ShowPlaylistsGroupScreen);
        SetPageContext(new ProgramsGroupViewModel());
    }

    public void ShowProgramsListScreen(GroupInfo group)
    {
        if (CurrentPlaylistName == null)
            return;

        SettingsModel.Group = group;
        SettingsModel.LastScreen = nameof(ProgramsListViewModel);
        TopPanelVisible(true, group.Name);
        SetBackButtonAction(() => ShowProgramsGroupScreen(CurrentPlaylistName));
        SetPageContext(new ProgramsListViewModel(group));
    }

    public void ShowPlayerScreen(M3UInfo program)
    {
        SettingsModel.Program = program;
        SettingsModel.LastScreen = nameof(PlayerViewModel);
        SetPageContext(new PlayerViewModel(program));
    }

    public void ShowSettingsScreen()
    {
        TopPanelVisible(true, "Settings");
        SetBackButtonAction(SelectScreen);
        SetPageContext(new SettingsViewModel());
    }

    public void TopPanelVisible(bool value, string title)
    {
        _mainWindowViewModel.IsTopPanelVisible = value;
        _mainWindowViewModel.TopPanelTitle = title;
        _mainWindowViewModel.ContentMargin = value ? new Avalonia.Thickness(14, 12) : new Avalonia.Thickness(0);
    }

    public void FullScreenToggle()
    {
        _mainWindowViewModel.OnFullScreenButtonClick();
    }

    public void CloseAppCommand()
    {
        _mainWindowViewModel.OnCloseAppButtonClick();
    }

    public void SetBackButtonAction(Action action)
    {
        _mainWindowViewModel.ButtonBackAction = action;
    }

    private void SetPageContext(object viewModel)
    {
        if (_mainWindowViewModel.CurrentViewModel is IDisposable disposable)
            disposable.Dispose();

        _mainWindowViewModel.CurrentViewModel = viewModel;
        SettingsModel.SaveSetttings();
    }

    public void Dispose()
    {
        if (_mainWindowViewModel.CurrentViewModel is IDisposable disposable)
            disposable.Dispose();

        if (PlayListsData != null)
        {
            foreach (var kv in PlayListsData)
            {
                try { kv.Value.Dispose(); } catch { }
            }
            PlayListsData.Clear();
        }
        if (CurrentProgramsData != null)
        {
            try { CurrentProgramsData.Dispose(); } catch { }
        }

        ProviderRegistry.Dispose();
    }
}
