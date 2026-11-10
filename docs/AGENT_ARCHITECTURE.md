# IPTVplayer architecture boundaries for coding agents

Source of truth: [README](../README.md), [regression tests](../TV%20Player%20Tests/README.md), [player smoke checks](player-smoke.md).

| Layer | Responsibility | Must not own |
| --- | --- | --- |
| TV Player Core | Parsing, channel identity/selection, stream resolution, EPG projection, shared presentation, state/commands and fallback policy | MAUI/Avalonia control focus, native UI layout |
| TV Player MAUI | Android/TV views, remote navigation, device lifecycle, native playback adapter and surface | Duplicated shared EPG or channel-selection policy |
| TV Player Avalonia | Desktop views/ViewModels, input/overlay behavior, native desktop video host | Android background/return policy |
| Provider plugins and abstractions | Source-specific discovery and credentials/headers | UI navigation or playback state machine |

## Integration invariants
- Selected channel title/EPG/source must not be overwritten by late callbacks from an older selection.
- EPG errors must not automatically fail a video stream.
- UI overlays and remote input follow the currently visible context; moving focus must not switch channels unless the user activates selection.
- Separate Android lifecycle (stop/reconnect upon background/return) from desktop lifecycle.
- Dispose native media and detach native video hosts in the established order.
- MAUI Android provider implementations must be compiled into the app; don't assume dynamic desktop DLL loading.
- Never include live signed media URLs, personal playlists or credentials in fixtures or PR logs.
