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

        private const string Guid = "ac8ab758-01e2-47c3-ad42-31e96d8203c0";
        static readonly Mutex Mutex = new Mutex(true, "{" + Guid + "}");
        protected override void OnStartup(StartupEventArgs e)
        {
            AppDomain.CurrentDomain.UnhandledException += ReportAndRestart;

            if (!Mutex.WaitOne(TimeSpan.Zero, true))
            {
                MessageBox.Show("Programm already running");
                Current.Shutdown();
            }

            _tvPlayer = new TVPlayerViewModel();

            base.OnStartup(e);
        }

        protected void ReportAndRestart(object sender, UnhandledExceptionEventArgs e)
        {

            string info = e.ExceptionObject.ToString();
            var result=MessageBox.Show(info, "Application", MessageBoxButton.OK, MessageBoxImage.Stop);
            
            //Environment.Exit(1);
        }
        protected override void OnExit(ExitEventArgs e)
        {
            _tvPlayer.Dispose();
            base.OnExit(e);
        }
    }
}
