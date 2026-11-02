using Android.App;
using Android.Content.PM;
using Android.Views;
using Microsoft.Maui;

namespace TV_Player.Maui;

[Activity(Theme = "@style/Maui.SplashTheme", MainLauncher = true, ConfigurationChanges = ConfigChanges.ScreenSize | ConfigChanges.Orientation | ConfigChanges.UiMode | ConfigChanges.ScreenLayout | ConfigChanges.SmallestScreenSize | ConfigChanges.Density)]
public class MainActivity : MauiAppCompatActivity
{
    private SystemUiFlags? _previousSystemUiFlags;

    public static event Action? AppPausing;
    public static event Action? AppResumed;

    protected override void OnPause()
    {
        AppPausing?.Invoke();
        SetPrivacyBlur(true);
        base.OnPause();
    }

    protected override void OnResume()
    {
        base.OnResume();
        SetPrivacyBlur(false);
        AppResumed?.Invoke();
    }

    private void SetPrivacyBlur(bool enabled)
    {
        if (!OperatingSystem.IsAndroidVersionAtLeast(31)) return;

        var decorView = Window?.DecorView;
        if (decorView == null) return;

        decorView.SetRenderEffect(enabled
            ? Android.Graphics.RenderEffect.CreateBlurEffect(18f, 18f, Android.Graphics.Shader.TileMode.Clamp)
            : null);
    }

    public void SetImmersiveMode(bool enabled)
    {
        if (OperatingSystem.IsAndroidVersionAtLeast(30))
        {
            var controller = Window?.InsetsController;
            if (controller is null) return;

            var systemBars = WindowInsets.Type.StatusBars() | WindowInsets.Type.NavigationBars();
            if (enabled)
            {
                controller.Hide(systemBars);
                controller.SystemBarsBehavior = (int)WindowInsetsControllerBehavior.ShowTransientBarsBySwipe;
            }
            else
            {
                controller.Show(systemBars);
            }

            return;
        }

        var decorView = Window?.DecorView;
        if (decorView is null) return;

        if (enabled)
        {
            _previousSystemUiFlags ??= decorView.SystemUiFlags;
            decorView.SystemUiFlags = SystemUiFlags.Fullscreen
                | SystemUiFlags.HideNavigation
                | SystemUiFlags.ImmersiveSticky
                | SystemUiFlags.LayoutStable
                | SystemUiFlags.LayoutFullscreen
                | SystemUiFlags.LayoutHideNavigation;
        }
        else if (_previousSystemUiFlags is { } previousFlags)
        {
            decorView.SystemUiFlags = previousFlags;
            _previousSystemUiFlags = null;
        }
    }
}
