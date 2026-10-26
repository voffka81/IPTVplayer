# IPTVplayer automated checks

Run with .NET 10 SDK:

```sh
dotnet test "TV Player Tests/TV Player Tests.csproj"
```

The tests exercise the shared navigation view model and local M3U parsing without network calls or starting VLC. GitHub Actions builds Avalonia and runs the tests on Windows and macOS for each PR. WPF is intentionally excluded from this cross-platform workflow.

These are initial smoke tests, not video playback or ROG Ally touch UI tests. Follow-up work: headless Avalonia visual-tree tests, player-to-settings navigation lifecycle tests, stream provider integration tests.
