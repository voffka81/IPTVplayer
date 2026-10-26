# Playback engine architecture

The playback API lives in `TV Player Core/Playback`, without references to Avalonia,
WPF, LibVLCSharp, or libmpv. This is an **initial architecture-only change**.

- `IPlaybackEngine` defines playback commands, state, volume and error events.
- `StreamSource` describes a stream URL, optional HTTP headers and fallbacks.
- `PlaybackEngineRegistry` registers lazy backend factories; registration itself
  does not load native libraries.
- An engine implementation owns and disposes its native player instance.

## Next steps (not implemented in this PR)

1. Implement a VLC adapter using the existing LibVLCSharp code.
2. Add an mpv adapter, with platform-specific video surface integration.
3. Bridge Avalonia and WPF video surfaces without exposing UI types in Core.
4. Wire engine selection into settings and migrate player views.
5. Add smoke tests for Windows and macOS, including native dependency loading.

Until those steps are done, the existing playback path remains unchanged.
This PR does **not** resolve current LibVLC native library loading errors.

## Branch integration

This branch is based on `master` and targets .NET 8 like the existing Core project.
The separate .NET 10 migration PR will update its target framework after merge.
