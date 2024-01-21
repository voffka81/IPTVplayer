using System.Windows;
using TV_Player.ViewModels;

namespace TV_Player
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        private TVPlayerViewModel _tvPlayer;
        protected override void OnStartup(StartupEventArgs e)
        {
            _tvPlayer = new TVPlayerViewModel();

            base.OnStartup(e);
        }

        protected override void OnExit(ExitEventArgs e)
        {
            _tvPlayer = null;
            base.OnExit(e);
        }
    }
}
