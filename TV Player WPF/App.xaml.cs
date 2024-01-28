using System.Windows;
using System.Windows.Controls.Primitives;
using TV_Player.ViewModels;

namespace TV_Player
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        private TVPlayerViewModel _tvPlayer;
       
        private const string Guid = "250C5597-BA73-40DF-B2CF-DD644F044834";
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
            MessageBox.Show(info,"Application");
            
            Environment.Exit(1);
        }
        protected override void OnExit(ExitEventArgs e)
        {
            _tvPlayer = null;
            base.OnExit(e);
        }
    }
}
