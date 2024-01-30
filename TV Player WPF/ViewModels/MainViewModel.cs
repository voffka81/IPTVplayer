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

        private WindowStyle _currentWindowStyle;
        public WindowStyle CurrentWindowStyle
        {
            get => _currentWindowStyle;
            set => SetProperty(ref _currentWindowStyle, value);
        }

        public ICommand FullscreenCommand { get; }

        public Action ButtonBackAction { get; set; }
        public ICommand BackCommand { get; }

        public ICommand SettingsCommand{ get; }

        public MainViewModel()
        {

            BackCommand = new RelayCommand(OnButtonBackClick);
            FullscreenCommand = new RelayCommand(OnFullSctreenButtonClick);
            SettingsCommand = new RelayCommand(OnSettingsButtonClick);

            CurrentWindowStyle = WindowStyle.SingleBorderWindow;
        }

        public void OnFullSctreenButtonClick()
        {
            if (CurrentWindowStyle == WindowStyle.SingleBorderWindow)
            {
                CurrentWindowStyle = WindowStyle.None;
                CurrentWindowState = WindowState.Maximized;
            }
            else
            {
                CurrentWindowStyle = WindowStyle.SingleBorderWindow;
                CurrentWindowState = WindowState.Normal;
            }
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
