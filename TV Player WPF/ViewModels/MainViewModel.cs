using CommunityToolkit.Mvvm.Input;
using System.Windows.Controls;
using System.Windows.Input;

namespace TV_Player
{
    public class MainViewModel : ObservableViewModelBase
    {
        private ContentControl _control;
        public ContentControl Control
        {
            get => _control;
            set => SetProperty(ref _control, value);
        }

        private bool _isTopPanelVisible;
        public bool IsTopPanelVisible{
            get => _isTopPanelVisible;
            set => SetProperty(ref _isTopPanelVisible, value);
        }

        private string _topPaneTitle;
        public string TopPanelTitle
        {
            get => _topPaneTitle;
            set => SetProperty(ref _topPaneTitle, value);
        }

        public Action ButtonBackAction { get; set; }
        public ICommand BackCommand { get; }

        public MainViewModel()
        {          
            BackCommand = new RelayCommand(OnButtonBackClick);
        }
        private void OnButtonBackClick()
        {
            ButtonBackAction?.Invoke();
        }
    }
}
