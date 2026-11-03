# IPTVplayer regression checks

Run with the .NET 10 SDK, without starting VLC or contacting real providers:

```sh
dotnet test "TV Player Tests/TV Player Tests.csproj" --configuration Release \
  --logger "trx;LogFileName=player-regression.trx" \
  --results-directory artifacts/test-results \
  -- RunConfiguration.TestSessionTimeout=120000
python -m unittest discover -s scripts -p "test_*.py"
```

The test session limit bounds a broken asynchronous scenario. It is not a playback
startup timeout. Provider replies use controlled tasks, clocks are supplied
explicitly, and unit tests do not wait for a real stream or decoded frame.

## Coverage

| Area | Main regression suites |
| --- | --- |
| Rapid switching, cancellation, queued stale replies and independent EPG loading | `StreamResolutionServiceTests`, `PlayerPresentationModelTests` |
| Foreground/surface ordering, stop/resume, recovery and disposal | `ForegroundPlaybackSessionTests`, `PlayerPlaybackModelTests`, `PlaybackRecoveryRegressionTests` |
| Composed presentation + foreground/surface contracts, exit/re-entry and repeated sessions | `PlayerIntegrationRegressionTests` |
| EPG boundaries, gaps, repeated titles, progress and guide windows | `ProgramGuideProjectionTests` |
| Overlay auto-hide, picker/EPG exclusivity and inactive/disposed behavior | `PlayerUiControllerTests`, `PlaybackUiStateTests` |
| Channel identity, selection, remote routing and fallback ordering | `ChannelSelectionStateTests`, `PlayerRemoteInputRouterTests`, `StreamFallbackQueueTests` |
| Update metadata, version/signature policy and actual APK packaging requirements | `AndroidUpdateTests`, `scripts/test_android_release.py`, `scripts/test_android_apk.py` |

The composed player harness exercises shared contracts in the same ordering as
the Android adapter. It does not instantiate MAUI/Avalonia controls, prove native
adapter wiring or decode video. WPF remains outside this cross-platform workflow.

## CI and artifacts

`Player regression` runs on every PR, pushes to `master`, and manual dispatches.
It builds/tests Avalonia on Windows and macOS and calls the reusable Android APK
workflow. No path filters can leave an otherwise required platform check absent.
The final **Player regression gate** succeeds only when the desktop matrix and
Android build, actual APK validation and artifact upload all succeed; skipped,
failed or cancelled dependencies cannot produce a successful gate.

Download from the Actions run's **Artifacts** section:

- `IPTVplayer-android-apk`: signed `IPTVplayer-android.apk`, retained for 30 days.
- `player-tests-windows-latest` and `player-tests-macos-latest`: TRX results,
  uploaded even after failed tests when a report exists, retained for 30 days.

Obsolete PR runs are cancelled; release workflows keep their separate concurrency
policy. The Android workflow remains independently runnable from Actions.
Artifacts are not update-channel releases; use a Dev release for in-app updates.

A workflow exposes checks but does not configure repository branch protection.
The former standalone `build-android` check name is preserved by a compatibility
job that requires the reusable Android build and upload to succeed, so existing
required-check names remain available. To use one required check instead, select
**Player regression gate** in branch protection/rulesets. Do not treat this
documentation as evidence that branch rules have been enabled.

Before merging a player refactor, also record the relevant
[manual player smoke checks](../docs/player-smoke.md). Native frames, audio,
fullscreen, platform focus and device installation need those checks.
