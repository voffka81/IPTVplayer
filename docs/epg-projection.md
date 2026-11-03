# Shared EPG projection (#66)

`Core/Presentation/ProgramGuideProjection.Project` accepts guide data and an
explicit clock value. It owns chronological ordering, current/next selection,
carousel windows, time labels and normalized progress. It has no UI framework,
VLC, timer, DateTime.Now or hidden culture dependency. Clients supply their local
clock/culture; all input times must use that same time domain.

A projected item is an immutable snapshot: title, start/end, current flag,
progress and both HH:mm and localized short-time labels. The selected current
program uses the half-open interval `start <= now < end`. At a boundary the next
program starts at zero progress; the preceding program is no longer current.
Overlaps select the first chronological active program for the summary and mark
all active cards. Duplicate titles are never used as identities. Null and invalid
zero/negative intervals are excluded. Progress is clamped to [0,1].

Clients retain their presentation differences:

- MAUI displays current or upcoming information (with its localized 'Next' prefix),
  HH:mm labels and a window beginning with current, next or the first available
  record. The carousel renders projected flags/progress without reading a clock.
- Avalonia hides the summary during a schedule gap and retains its current-or-first
  carousel policy. Its summary uses localized short time, cards use HH:mm.

Both consume the same projection contract. An all-expired guide retains initial
cards but has no current/next summary. The default window is seven items; no guide
or an empty guide produces an empty state. Projection does not mutate source data.

MAUI advances its card window when a minute refresh crosses a program boundary,
keeping existing controls/focus when identities are unchanged and retaining the
focused program when it remains in a new window. This fixes a previously frozen
card window. Avalonia no longer jumps to an older same-title program. Normal
palette, layout, visibility policy and time formats remain unchanged.

Tests use fixed explicit times and cover exact start/end, gaps, overlaps, expired
schedules, repeated titles, unordered input, window limits, malformed intervals,
midnight/subsecond progress, locale labels and snapshot independence. Native focus
on a phone/TV should be checked when an open carousel advances to a new program.
