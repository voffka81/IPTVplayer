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

## Android updates

Open **Settings → App updates** to choose the **Release** (default) or **Dev**
channel, check for a newer version, download its APK and open Android's installer.
Startup checks are non-blocking and never download/install automatically.
The first app version containing this feature must be installed manually.

The unified release workflow publishes signed APKs with `android-update.json`
and increasing Android version codes. Dev builds are GitHub prereleases; stable
builds are normal releases. PR artifacts are for testing and are not offered by
the updater. See [Android update distribution and device checks](docs/android-updates.md).

## Player presentation ownership

Both clients render `PlayerPresentationModel` from Core. It owns channel selection,
channel title, stream/EPG request coordination, the projected guide and playback
status codes. A channel change resets the source and guide together; stream and
EPG requests run independently, and stale results are checked again after UI dispatch.
Guide readiness notifications coalesce with a pending load; a guide error does not
fail the video stream. Clients supply the dispatcher, clock, culture and guide display
options (including whether an upcoming program is shown before the next broadcast).

The MAUI page and Avalonia ViewModels retain localized text, bindings, focus,
overlays, platform navigation and minute timers. Native adapters retain LibVLC,
video surfaces and foreground/surface lifecycle handling. Dispose the presentation
model when leaving the player permanently; source providers remain owned by
`ProgramsData`.
