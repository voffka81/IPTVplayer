using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.Input;
using System.Windows.Input;

namespace TV_Player.AvaloniaApp.ViewModels;

public class MainWindowViewModel : TV_Player.ObservableViewModelBase
{
    private object? _currentViewModel;
    public object? CurrentViewModel
    {
        get => _currentViewModel;
        set
        {
            if (SetProperty(ref _currentViewModel, value))
            {
                OnPropertyChanged(nameof(IsSettingsButtonVisible));
                OnPropertyChanged(nameof(IsBackButtonVisible));
            }
        }
    }

    private Thickness _contentMargin = new(14, 12);
    public Thickness ContentMargin
    {
        get => _contentMargin;
        set => SetProperty(ref _contentMargin, value);
    }

    private bool _isTopPanelVisible;
    public bool IsTopPanelVisible
    {
        get => _isTopPanelVisible;
        set => SetProperty(ref _isTopPanelVisible, value);
    }

    public bool IsSettingsButtonVisible => CurrentViewModel is not SettingsViewModel;
    public bool IsBackButtonVisible => CurrentViewModel is not PlaylistsGroupViewModel;

    private readonly DispatcherTimer _clockTimer = new() { Interval = TimeSpan.FromSeconds(10) };
    private string _clockText = DateTime.Now.ToString("HH:mm");
    public string ClockText { get => _clockText; private set => SetProperty(ref _clockText, value); }

    private string _topPanelTitle = string.Empty;
    public string TopPanelTitle
    {
        get => _topPanelTitle;
        set => SetProperty(ref _topPanelTitle, value);
    }

    private WindowState _currentWindowState = WindowState.Normal;
    public WindowState CurrentWindowState
    {
        get => _currentWindowState;
        set => SetProperty(ref _currentWindowState, value);
    }

    public ICommand FullscreenCommand { get; }
    public ICommand CloseAppCommand { get; }
    public ICommand BackCommand { get; }
    public ICommand SettingsCommand { get; }

    public Action? ButtonBackAction { get; set; }

    public MainWindowViewModel()
    {
        BackCommand = new RelayCommand(TriggerBack);
        FullscreenCommand = new RelayCommand(OnFullScreenButtonClick);
        SettingsCommand = new RelayCommand(OnSettingsButtonClick);
        CloseAppCommand = new RelayCommand(OnCloseAppButtonClick);
        _clockTimer.Tick += (_, _) => ClockText = DateTime.Now.ToString("HH:mm");
        _clockTimer.Start();
    }

    public void OnFullScreenButtonClick()
    {
        CurrentWindowState = CurrentWindowState == WindowState.FullScreen
            ? WindowState.Normal
            : WindowState.FullScreen;
    }

    public void OnCloseAppButtonClick()
    {
        if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.Shutdown();
        }
    }

    private void OnSettingsButtonClick()
    {
        TVPlayerViewModel.Instance.ShowSettingsScreen();
    }

    public void TriggerBack()
    {
        ButtonBackAction?.Invoke();
    }
}
