using TV_Player.Maui.Services;

namespace TV_Player.Maui;

public partial class App : Application
{
    public App()
    {
        InitializeComponent();
        MainPage = new NavigationPage(new PlaylistPage(AppSession.Current))
        {
            BarBackgroundColor = Color.FromArgb("#080F19"),
            BarTextColor = Colors.White
        };
    }
}
