namespace TV_Player.MAUI
{
    public partial class AppShell : Shell
    {
        private TVPlayerViewModel _tvPlayer;
        public AppShell()
        {
            InitializeComponent();
            _tvPlayer = new TVPlayerViewModel();
        }
    }
}
