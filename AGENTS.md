# IPTVplayer agent instructions

Read [README.md](README.md), [architecture](docs/AGENT_ARCHITECTURE.md), [validation](docs/AGENT_VALIDATION.md), [test guide](TV%20Player%20Tests/README.md), and [player smoke checks](docs/player-smoke.md) before changing the player.

## Boundaries
- `TV Player Core`: shared M3U, channel/selection, stream resolution, EPG, playback presentation and provider contracts.
- `TV Player MAUI`: Android UI, TV remote/focus, device lifecycle, native LibVLC and APK integration.
- Avalonia desktop: platform-specific UI, native video host and desktop input; don't accidentally change Android behavior.
- Do not duplicate Core rules in UI clients. Maintain generation/cancellation guards and platform-specific lifecycle behavior.
- Keep provider identifiers, credentials, signed URLs and access tokens out of tests and logs.
## Agent workflow
- Work only on the explicitly assigned GitHub Issue; discuss unclear requirements rather than inventing a new feature.
- Do not initiate, delegate to, or spawn another coding agent without explicit owner approval.
- Keep changes in a dedicated branch and submit a PR; never merge, deploy, publish releases, or modify repository settings without explicit approval.
- Do not silently alter unrelated files. Preserve existing architecture; document necessary deviations in the PR.
- Run the smallest relevant checks first, then required CI. Do not repeatedly rerun identical failing commands without changing the underlying cause.
- After two unsuccessful repair attempts, stop and report the failing command, relevant log excerpt, files changed, and likely root cause.
- Report which checks actually ran and which did not. A passing build does not prove visual or physical-device behavior.

## Verification
- For Core changes run targeted .NET tests first; see `TV Player Tests/README.md`.
- CI's `Player regression gate` includes desktop tests and a real Android APK build. Do not claim Android TV remote or video works from CI alone.
- Check manual scenarios in `docs/player-smoke.md` and `docs/android-remote-smoke.md` where applicable.
