# Shared player architecture — migration plan

## Non-negotiable boundaries
- **Core/Application (platform-neutral):** playlist loading, provider resolution, EPG, playback session state machine, navigation commands, focus context, caching, cancellation and diagnostics.
- **Playback adapters:** platform-specific LibVLC / optional MPV implementation behind `IPlaybackEngine`. Native player objects never leak into shared ViewModels.
- **Presentation contracts:** `PlayerScreenModel`, `ProgramGuideModel`, `ChannelCarouselModel`, `PlaybackStatusModel`; reusable commands, selection, and focus semantics.
- **Platform Views:** Avalonia AXAML and MAUI XAML render the same models. No cross-framework XAML reuse or framework references in Core.
- **Platform services:** XInput, keyboard hook, touch gestures, system volume and app lifecycle behind interfaces.

## Proposed modules
1. `TV Player Core`: existing M3U/XMLTV/providers, refactored network/cache services.
2. `TV Player Application`: playback orchestration and shared presentation models (new framework-free .NET library).
3. `TV Player Avalonia`: desktop views, input/volume adapters and video host.
4. `TV Player MAUI`: Android views, touch/input adapters and video host.
5. `TV Player Tests`: shared-state tests and provider/network test doubles.

## Migration sequence (small, reviewable PRs)
1. Finish bottom EPG PR #31; ensure responsive width and context-aware D-pad; test touch and desktop.
2. Extract `InputRouter` and `IInputSource` from `PlayerView.axaml.cs`; cover playback vs EPG vs channel picker with unit tests.
3. Extract `IPlaybackEngine` + `PlaybackSession` from `PlayerView`; explicit states (Idle, Resolving, Buffering, Playing, Recovering, Error, Disposed); single-flight cancellation.
4. Move EPG loading into shared `ProgramGuideController`: stale-response protection, one refresh scheduler, UI-independent state.
5. Introduce shared component presentation models, migrate both UIs incrementally, remove duplicated logic only after parity tests.
6. Extract platform audio and overlay services; delete legacy WPF only after inventory and release verification.
7. Replace constructor fire-and-forget playlist loading; add HTTP client ownership, cache policy and bounded streaming XMLTV parsing.

## Acceptance gates for every PR
- Windows and macOS Avalonia CI build and existing tests pass.
- Android MAUI build passes when shared or Android code changes.
- No channel change when navigating EPG; no stale EPG after rapid channel changes.
- No growth in active timers/subscriptions across repeated channel switching.
- No regression in gamepad-only navigation on ROG Ally.
- No new framework-specific dependencies in shared code.
- Compare steady-state memory and repeated playback startup/shutdown before/after refactor.

## Anti-goals
- No giant rewrite or simultaneous replacement of both frontends.
- No shared XAML via brittle transpilation or conditional markup.
- No DI container introduced solely for ceremony; favor explicit constructor injection.
- No claim of 'zero memory leaks' without profiling and repeated-run evidence.
