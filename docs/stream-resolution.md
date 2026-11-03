# Shared stream resolution (#69)

`Core/Providers/StreamResolutionService` owns provider lookup, request generations,
linked cancellation and suppression of obsolete responses. It accepts either an
`IChannelProvider` or an injected resolver delegate; it never owns/disposes the
provider. It has no MAUI, Avalonia or LibVLC dependency.

Both clients consume the same result contract:

- `Resolved`: URL, a snapshot of ordered fallback URLs, and optional expiry.
- `NoStream` / `ProviderFailed`: structured failure; the original provider exception
  is retained for diagnostics. User-facing strings are formatted by the client.
- `Cancelled` / `Superseded` / `Disposed`: suppress publication and playback.

Existing source policy is explicit. MAUI uses `WhenUrlMissing`, retaining a direct
M3U/provider URL when present; Avalonia uses `PreferProvider` to refresh a matching
provider/channel identity even when a URL is already set. Provider/channel IDs
must match and be nonempty. URL header suffixes remain intact. Avalonia retains
fallback playback; MAUI receives the contract without adding new fallback policy.

Starting a new request cancels the preceding one. Cancellation returns without
waiting for a provider that ignores its token; eventual faults are observed, and
late data cannot replace the latest selection. Cancellation callbacks run outside
the state lock. Each client also calls `IsCurrent(result)` immediately before
rendering: a successful result may become stale while waiting for the UI thread.
External cancellation after completion, explicit Cancel and Dispose all invalidate
such queued results. Dispose is idempotent and prevents future requests.

Deterministic tests use injected delegates and TaskCompletionSource, with no
network, VLC or wall-clock sleeps. Coverage includes direct/provider modes,
identity mismatch, fallbacks/expiry, provider failures, out-of-order responses,
non-cooperative cancellation, late errors, UI dispatch freshness and disposal.
