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

The **Player regression** workflow runs desktop tests and the reusable **Android APK build** workflow on every PR and push to `master`. Its final **Player regression gate** requires both platforms to succeed. The Android workflow can also run manually. Download the signed `IPTVplayer-android.apk` (`IPTVplayer-android-apk`) and desktop TRX reports from the completed Actions run's **Artifacts** section; these artifacts are retained for 30 days and are not GitHub Releases.

See [automated checks and CI artifacts](TV%20Player%20Tests/README.md) and the [quick player smoke checklist](docs/player-smoke.md).

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

The desktop `AvaloniaPlaybackController` owns native VLC subscriptions, media/engine,
startup reconciliation and fallback/watchdog handling. `PlayerView` retains layout,
input/audio widgets and the video host; `PlaybackHostLease` detaches that host before
releasing the adapter and MediaPlayer, including failed initialization. Queued native
callbacks are rejected after adapter disposal or a model/attempt change.
`MauiPlaybackController` owns Android player/media resources and detach-only video
binding; the page renders presentation snapshots and supplies the native surface.
See [current ownership and migration history](docs/shared-player-architecture.md)
and [playback adapters](docs/playback-engines.md).
