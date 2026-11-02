using Android.App;
using Android.Runtime;
using Microsoft.Maui;
using Microsoft.Maui.Hosting;

namespace TV_Player.Maui;

[Application]
public class MainApplication : MauiApplication
{
    public MainApplication(nint handle, Android.Runtime.JniHandleOwnership ownership) : base(handle, ownership) { }
    protected override MauiApp CreateMauiApp() => MauiProgram.CreateMauiApp();
}
