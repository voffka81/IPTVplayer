using TV_Player.ViewModels;

namespace TV_Player.AvaloniaApp.ViewModels;

public class TVPlayerViewModel : IDisposable
{
    private static readonly Lazy<TVPlayerViewModel> LazyInstance = new(() => new TVPlayerViewModel());
    public static TVPlayerViewModel Instance => LazyInstance.Value;

    private readonly MainWindowViewModel _mainWindowViewModel;

    public MainWindowViewModel MainWindowViewModel => _mainWindowViewModel;
    public ProgramsData? CurrentProgramsData { get; private set; }
    public Dictionary<string, ProgramsData> PlayListsData { get; } = new();
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

    public void InitializeTVWithData()
    {
        if (SettingsModel.Playlists is { Count: > 0 })
        {
            PlayListsData.Clear();
            foreach (var playlist in SettingsModel.Playlists)
            {
                PlayListsData[playlist.Key] = new ProgramsData(playlist.Key, playlist.Value);
            }

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
        TopPanelVisible(false, string.Empty);
        SetPageContext(new SettingsViewModel());
    }

    public void TopPanelVisible(bool value, string title)
    {
        _mainWindowViewModel.IsTopPanelVisible = value;
        _mainWindowViewModel.TopPanelTitle = title;
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
    }
}
