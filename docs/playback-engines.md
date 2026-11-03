# Playback engine architecture

The playback API lives in `TV Player Core/Playback` without dependencies on Avalonia or WPF.

## Current clients

Avalonia uses `LibVlcPlaybackEngine` behind `AvaloniaPlaybackController`; its view
owns the native host/MediaPlayer through `PlaybackHostLease`. Native Playing and
Buffering events, rather than an accepted backend Play command, drive the shared
presentation snapshot. The adapter retains ordered URL fallbacks, the 12-second
startup watchdog and the compatibility retry options.

MAUI uses `MauiPlaybackController` with the shared foreground/surface contracts.
It stops/disposes native player/media in the background and starts a fresh live
connection after foreground and surface readiness. Both clients bind the Core
`PlayerPresentationModel` and `PlayerPlaybackModel`.

## Engine API and optional MPV backend

- `IPlaybackEngine`: playback commands, state, volume and error events.
- `StreamSource`: URL, optional headers and fallback URLs.
- `PlaybackEngineRegistry`: lazy factories; registration does not load native libraries.
- `IVideoSurfacePlaybackEngine`: supplies a native window handle before playback.
- `MpvPlaybackEngine`: initial libmpv native backend using P/Invoke for initialization,
  loadfile, stop, pause, volume, and native window output (`wid`).

Example of backend registration and use (requires a valid native child-window handle):

```csharp
var engines = new PlaybackEngineRegistry();
engines.Register("mpv", () => new MpvPlaybackEngine());

await using var engine = engines.Create("mpv");
((IVideoSurfacePlaybackEngine)engine).SetVideoOutputHandle(nativeChildWindowHandle);
await engine.PlayAsync(new StreamSource("https://example.org/live.m3u8"));
```

## Optional MPV limitations

- The optional MPV backend is not integrated into the client views; current clients
  use VLC as described above.
- The application must deploy a compatible native libmpv library. Native dependency
  packaging and cross-platform loader verification remain outstanding.
- PlaybackState.Playing currently means libmpv accepted `loadfile`, **not** that
  frames are playing. Proper event-based state reporting is still required.
- HTTP headers, fallback URLs, cancellation after command dispatch, automatic
  reconnection and advanced error reporting are not implemented.
- No build or Windows/macOS runtime smoke test has been performed in this environment.
- Native `wid` embedding requires platform-specific host window lifecycle handling;
  macOS support must be validated separately.

## Optional MPV follow-up

1. Integrate a native video surface host into Avalonia and WPF.
2. Preserve VLC native event/surface semantics when offering another engine.
3. Wire engine selection into settings and handle runtime failures.
4. Add event-based mpv state/error tracking, stream headers and fallback logic.
5. Build and test on Windows/macOS.

The applications target .NET 10. Native engine/device smoke checks remain
separate from the framework-free regression suite.
