using Android.App;
using Android.Content.PM;
using Android.Graphics;
using Android.OS;
using Android.Views;
using Android.Widget;
using Microsoft.Maui;
using NativeView = Android.Views.View;

namespace TV_Player.Maui;

[Activity(Theme = "@style/Maui.SplashTheme", MainLauncher = true, ConfigurationChanges = ConfigChanges.ScreenSize | ConfigChanges.Orientation | ConfigChanges.UiMode | ConfigChanges.ScreenLayout | ConfigChanges.SmallestScreenSize | ConfigChanges.Density)]
public class MainActivity : MauiAppCompatActivity
{
    private SystemUiFlags? _previousSystemUiFlags;
    private bool _isBackgrounded;
    private bool _pixelCopyPending;
    private ImageView? _privacySnapshotView;
    private Bitmap? _privacySnapshotBitmap;
    private PixelCopyFinishedListener? _pixelCopyListener;

    public static event Action? AppPausing;
    public static event Action? AppResumed;

    protected override void OnPause()
    {
        _isBackgrounded = true;
        AppPausing?.Invoke();
        if (!_pixelCopyPending && _privacySnapshotView == null)
            CaptureBlurredVideoFrame();
        base.OnPause();
    }

    protected override void OnUserLeaveHint()
    {
        _isBackgrounded = true;
        CaptureBlurredVideoFrame();
        base.OnUserLeaveHint();
    }

    protected override void OnResume()
    {
        base.OnResume();
        _isBackgrounded = false;
        RemovePrivacySnapshot();
        AppResumed?.Invoke();
    }

    private void CaptureBlurredVideoFrame()
    {
        if (!OperatingSystem.IsAndroidVersionAtLeast(24) || _pixelCopyPending) return;

        var videoView = FindVideoSurface(Window?.DecorView);
        if (videoView == null || videoView.Width <= 0 || videoView.Height <= 0 || !videoView.Holder.Surface.IsValid)
            return;

        var bitmap = Bitmap.CreateBitmap(
            Math.Max(1, videoView.Width / 2),
            Math.Max(1, videoView.Height / 2),
            Bitmap.Config.Argb8888);
        _pixelCopyPending = true;
        var listener = new PixelCopyFinishedListener(result =>
        {
            _pixelCopyPending = false;
            _pixelCopyListener = null;
            RunOnUiThread(() =>
            {
                if (!_isBackgrounded || (PixelCopyResult)result != PixelCopyResult.Success || IsFinishing || IsDestroyed ||
                    !OperatingSystem.IsAndroidVersionAtLeast(31))
                {
                    bitmap.Recycle();
                    return;
                }

                ShowPrivacySnapshot(bitmap);
            });
        });
        _pixelCopyListener = listener;

        try
        {
            PixelCopy.Request(videoView, bitmap, listener, new Handler(Looper.MainLooper!));
        }
        catch (Exception ex)
        {
            _pixelCopyPending = false;
            _pixelCopyListener = null;
            bitmap.Recycle();
            System.Diagnostics.Debug.WriteLine($"Could not capture video frame for app-switch preview: {ex.Message}");
        }
    }

    private void ShowPrivacySnapshot(Bitmap bitmap)
    {
        var decorView = Window?.DecorView as ViewGroup;
        if (decorView == null)
        {
            bitmap.Recycle();
            return;
        }

        RemovePrivacySnapshot();
        var imageView = new ImageView(this);
        imageView.SetScaleType(ImageView.ScaleType.CenterCrop);
        imageView.Clickable = true;
        imageView.Focusable = true;
        imageView.SetImageBitmap(bitmap);
        if (OperatingSystem.IsAndroidVersionAtLeast(31))
        {
            imageView.SetRenderEffect(RenderEffect.CreateBlurEffect(
                24f, 24f, Android.Graphics.Shader.TileMode.Clamp));
        }
        decorView.AddView(imageView, new ViewGroup.LayoutParams(
            ViewGroup.LayoutParams.MatchParent,
            ViewGroup.LayoutParams.MatchParent));
        imageView.BringToFront();
        _privacySnapshotView = imageView;
        _privacySnapshotBitmap = bitmap;
    }

    private void RemovePrivacySnapshot()
    {
        if (_privacySnapshotView?.Parent is ViewGroup parent)
            parent.RemoveView(_privacySnapshotView);
        _privacySnapshotView?.SetImageBitmap(null);
        _privacySnapshotView?.Dispose();
        _privacySnapshotView = null;
        if (_privacySnapshotBitmap is { IsRecycled: false } bitmap)
            bitmap.Recycle();
        _privacySnapshotBitmap = null;
    }

    private static LibVLCSharp.Platforms.Android.VideoView? FindVideoSurface(NativeView? view)
    {
        if (view is LibVLCSharp.Platforms.Android.VideoView videoView)
            return videoView;

        if (view is not ViewGroup group)
            return null;

        for (var index = 0; index < group.ChildCount; index++)
        {
            var match = FindVideoSurface(group.GetChildAt(index));
            if (match != null)
                return match;
        }

        return null;
    }

    private sealed class PixelCopyFinishedListener : Java.Lang.Object, PixelCopy.IOnPixelCopyFinishedListener
    {
        private readonly Action<int> _onFinished;

        public PixelCopyFinishedListener(Action<int> onFinished) => _onFinished = onFinished;

        public void OnPixelCopyFinished(int copyResult) => _onFinished(copyResult);
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
