# Playback engine architecture

The playback API lives in `TV Player Core/Playback` without dependencies on Avalonia or WPF.

## Implemented

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

## Known limitations

- This is **not yet integrated into the Avalonia or WPF views**. Existing playback
  remains unchanged. It does not resolve the current Avalonia VideoView exception.
- The application must deploy a compatible native libmpv library. Native dependency
  packaging and cross-platform loader verification remain outstanding.
- PlaybackState.Playing currently means libmpv accepted `loadfile`, **not** that
  frames are playing. Proper event-based state reporting is still required.
- HTTP headers, fallback URLs, cancellation after command dispatch, automatic
  reconnection and advanced error reporting are not implemented.
- No build or Windows/macOS runtime smoke test has been performed in this environment.
- Native `wid` embedding requires platform-specific host window lifecycle handling;
  macOS support must be validated separately.

## Follow-up

1. Integrate a native video surface host into Avalonia and WPF.
2. Adapt existing VLC playback to `IPlaybackEngine`.
3. Wire engine selection into settings and handle runtime failures.
4. Add event-based mpv state/error tracking, stream headers and fallback logic.
5. Build and test on Windows/macOS.

This branch is based on `master`. .NET 10 migration is handled separately.
