using CommunityToolkit.Mvvm.Input;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using TV_Player.ViewModels;

namespace TV_Player
{
    public class MainViewModel : ObservableViewModelBase
    {
        private ContentControl? _control;
        public ContentControl? Control
        {
            get => _control;
            set => SetProperty(ref _control, value);
        }
       

        private bool _isTopPanelVisible;
        public bool IsTopPanelVisible
        {
            get => _isTopPanelVisible;
            set => SetProperty(ref _isTopPanelVisible, value);
        }

        private string _topPaneTitle;
        public string TopPanelTitle
        {
            get => _topPaneTitle;
            set => SetProperty(ref _topPaneTitle, value);
        }

        private WindowState _currentWindowState;
        public WindowState CurrentWindowState
        {
            get => _currentWindowState;
            set => SetProperty(ref _currentWindowState, value);
        }

        public ICommand FullscreenCommand { get; }
        public ICommand CloseAppCommand { get; }

        public Action ButtonBackAction { get; set; }
        public ICommand BackCommand { get; }

        public ICommand SettingsCommand{ get; }

        public MainViewModel()
        {

            BackCommand = new RelayCommand(OnButtonBackClick);
            FullscreenCommand = new RelayCommand(OnFullSctreenButtonClick);
            SettingsCommand = new RelayCommand(OnSettingsButtonClick);
            CloseAppCommand = new RelayCommand(OnCloseAppButtonClick);
        }

        public void OnFullSctreenButtonClick()
        {
            if (CurrentWindowState == WindowState.Normal)
            {
                CurrentWindowState = WindowState.Maximized;
            }
            else
            {
                CurrentWindowState = WindowState.Normal;
            }
        }

        private void OnCloseAppButtonClick()
        {
            Environment.Exit(0);
        }

        private void OnButtonBackClick()
        {

            ButtonBackAction?.Invoke();
        }

        private void OnSettingsButtonClick()
        {
            TVPlayerViewModel.Instance.ShowSettingsScreen();
        }
    }
}
