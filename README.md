# IPTV Player

IPTV player with a shared .NET core and separate user interfaces.

## Projects

- **TV Player Avalonia** — desktop application for Windows, macOS, and Linux.
- **TV Player MAUI** — Android application, sharing playlist and channel data logic with the desktop app.
- **TV Player Core** — M3U parsing, channel models, playlist loading, EPG, and provider abstractions.
- **TV Player Plugin.Abstractions** — contracts used by provider plugins.

The Android client supports M3U playlists and includes the Viks.TV and Klubnichka.live providers in the APK. Additional desktop provider DLLs are not dynamically loaded on Android; providers need to be referenced and compiled into the Android app.

## Android development

Install the .NET MAUI Android workload, Android SDK, and JDK, then build with:

```powershell
dotnet build "TV Player MAUI/TV Player MAUI.csproj" -f net10.0-android
```

The Android app uses LibVLC for playback and the same dark channel tile palette as the Avalonia interface.

The separate **Android APK build** workflow runs for Android-related pull requests and pushes to `master`, or manually from the GitHub Actions page. It uploads `IPTVplayer-android.apk` as the `IPTVplayer-android-apk` workflow artifact for 30 days; it does not attach the APK to a GitHub Release. Download it from the completed Actions run's **Artifacts** section.
