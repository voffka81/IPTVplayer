using CommunityToolkit.Mvvm.Input;
using System.Windows.Controls;
using System.Windows.Input;

namespace TV_Player.ViewModels
{
    public class TVPlayerViewModel
    {
        private readonly MainViewModel _mainViewModel;
        
        public Action ButtonBackAction { get; set; }       

        private static TVPlayerViewModel _instance;
        public static TVPlayerViewModel Instance
        {
            get
            {
                if(_instance==null)
                    _instance = new TVPlayerViewModel();
                return _instance;
            }
        }

        public TVPlayerViewModel()
        {
            _mainViewModel = new MainViewModel();
            var mainWindow=new MainWindow();
            mainWindow.DataContext = _mainViewModel;
            
            mainWindow.Show();
            _instance = this;
           
        }
        public void TopPanelVisible(bool value)
        {
            _mainViewModel.IsTopPanelVisible = value;
        }

        public void SetBackButtonAction(Action action)
        {
            _mainViewModel.ButtonBackAction = action;
        }

        public void SetPageContext(ContentControl control, object viewModel)
        {
            control.DataContext = viewModel;
            _mainViewModel.Control = control;
        }
    }
}
