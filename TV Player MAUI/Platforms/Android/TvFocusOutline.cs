using Android.Graphics.Drawables;
using Microsoft.Maui.Handlers;
using NativeColor = Android.Graphics.Color;

namespace TV_Player.Maui;

// Draw focus above the content without replacing button backgrounds or MAUI visual states.
internal static class TvFocusOutline
{
    public static void Register()
    {
        ViewHandler.ViewMapper.AppendToMapping("TvFocusOutline", (handler, view) =>
        {
            if (!OperatingSystem.IsAndroidVersionAtLeast(23) ||
                view is not (Button or Entry or Picker))
                return;

            var native = handler.PlatformView;
            var density = native.Resources?.DisplayMetrics?.Density ?? 1f;
            int Dp(float value) => Math.Max(1, (int)Math.Round(value * density));
            GradientDrawable Ring(string color, int width)
            {
                var ring = new GradientDrawable();
                ring.SetColor(NativeColor.Transparent);
                ring.SetCornerRadius(Dp(12));
                ring.SetStroke(Dp(width), NativeColor.ParseColor(color));
                return ring;
            }

            // Concentric translucent rings keep the halo within the control bounds,
            // so collection view clipping cannot hide it.
            var focused = new LayerDrawable(new Drawable[]
            {
                Ring("#3355B9FF", 9),
                Ring("#8855B9FF", 6),
                Ring("#FFFFD600", 3)
            });
            var states = new StateListDrawable();
            states.AddState(new[] { Android.Resource.Attribute.StateFocused }, focused);
            states.AddState(Array.Empty<int>(), new ColorDrawable(NativeColor.Transparent));
            native.Foreground = states;
        });
    }
}
