# Android player remote smoke checks

The player uses Core `PlayerUiController` and `ChannelSelectionState` for overlay
policy and channel requests. `MauiPlayerUiController` owns the dispatcher timer;
Android `MainActivity` translates keys only while the player is subscribed.
Native LibVLC ownership remains in `MauiPlaybackController`.

## Player keys

| Context | Left / Right | Up / Down | OK / Enter | Back |
| --- | --- | --- | --- | --- |
| Playback | Previous / next channel, wrapping | Open EPG / channel picker | Open picker | Leave player |
| Channel picker | Move focus, wrapping | Move focus, wrapping | Native button selects channel | Close picker |
| EPG | Move between program cards | Move between program cards | Close EPG | Close EPG |

Focused native controls retain their own keys outside an overlay. Opening an
overlay moves focus to the selected channel or first program. Selection and
focus are distinct: moving through tiles does not start another stream.
Phone touch actions remain available; programmatic focus is used on TV or after
remote/keyboard input. Controls stay visible while an overlay is open, and hide
four seconds after it closes. Backgrounding and page exit stop the UI timer and
invalidate pending focus requests; resuming restores overlay focus.

## Device checks required before claiming Android TV support

Run on a phone and Xiaomi Android TV 14 with the same test playlist:

1. Install the APK; open playlist, group and channel using the remote. Confirm
   the launcher and native LibVLC ABI work on the device (tracked separately in #73).
2. Start a stream in fullscreen. Left/Right switches one channel at a time and
   wraps at the ends. Hold an arrow to switch quickly; the last request wins.
3. Down or OK opens the picker with the current tile focused and scrolled into
   view. Move across the list and verify focus remains visible. Playback changes
   only after OK. Reopen the picker and verify the new current channel is selected.
4. Up opens EPG when program information is available. Arrows scroll programs
   without switching playback. OK/Back closes EPG. Opening the picker closes EPG
   and vice versa (also exercise the touch controls on the phone).
5. Leave either overlay open for more than four seconds: bars remain visible.
   Close it and verify the bars disappear after four seconds. Tap video to hide
   and show bars on the phone.
6. Hold OK and Back: one key press must not reopen a panel or leave multiple
   pages. A Back press closes the panel first; a subsequent Back leaves playback.
7. Background/resume with each overlay open and closed. Verify video resumes,
   remote focus is usable and no delayed focus jumps occur after returning.
8. Exit/re-enter repeatedly, including immediately after opening a long picker
   and while resolving a channel. Verify there are no delayed UI callbacks,
   duplicate channel changes or timer/subscription growth.

Automated Core and Avalonia tests cover policy and selection, not native Android
focus, installation or actual video playback. Record device results in the PR;
#65/#75 and Android TV release acceptance remain pending those checks.

## Video surface startup regression

The MAUI player now hosts a plain LibVLC `VideoView`. Playback requests wait until
its Android handler, valid SurfaceHolder surface and nonzero native size are
available while the page is visible and the app is active. The latest queued
channel wins. The native VLC window size is supplied immediately after binding
(and rebinding), before Play, rather than relying on a later layout event.

On a phone, check a direct M3U channel on first opening the player, then a provider
channel with delayed URL resolution. Verify actual video and audio, switch rapidly
before the first frame, rotate the phone, background/resume, and exit/re-enter.
A stream-open error must remain visible instead of immediately clearing status;
no queued startup should execute after leaving the player. Automated surface-gate
tests cover both orderings of stream/surface readiness and repeated callbacks;
they cannot verify decoded frames on a physical Android device.
