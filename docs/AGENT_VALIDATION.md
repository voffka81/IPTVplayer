# Agent validation and PR evidence

## General
1. Read the linked Issue and acceptance criteria before modifying code.
2. Capture the baseline (branch, relevant failing test or reproduction).
3. Run focused tests/checks for touched code first.
4. Run the repository's normal build/test CI checks before claiming readiness; link the workflow run.
5. In the PR record: changed areas, test commands and results, manual checks with PASS/FAIL/NOT TESTED, limitations, and remaining risks.
6. If environment, SDK, assets or device is unavailable, state NOT TESTED rather than claiming success.
7. If a command fails twice for the same reason, stop and report diagnostics instead of looping.
8. Never include access tokens, signed stream URLs, secrets or personal information in logs or PR descriptions.
9. Require owner review before merge, release, deployment or cross-project changes.

## IPTVplayer-specific commands
Run focused Core regression tests when relevant:
```sh
dotnet test "TV Player Tests/TV Player Tests.csproj" --configuration Release
python -m unittest discover -s scripts -p "test_*.py"
```
For Android build when SDK/workloads are available:
```sh
dotnet build "TV Player MAUI/TV Player MAUI.csproj" -f net10.0-android
```
- Before merging player changes, require the existing `Player regression gate` workflow and inspect its artifact/check outcome; do not relax or skip required checks to get a green PR.
- Record device and input type and PASS/FAIL/NOT TESTED for `docs/player-smoke.md`. For TV remote, also use `docs/android-remote-smoke.md`.
- An APK build proves packaging, not installation, decoder output, sound, D-pad focus or TV device behavior.
- Distinguish provider outage from player defects using known-good test sources without exposing signed URLs.
