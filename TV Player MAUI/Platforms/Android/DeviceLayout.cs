namespace TV_Player.Maui;

internal static class DeviceLayout
{
    public static bool IsTelevision =>
        (Android.App.Application.Context.Resources?.Configuration?.UiMode &
         Android.Content.Res.UiMode.TypeMask) == Android.Content.Res.UiMode.TypeTelevision;
}
