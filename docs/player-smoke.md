# Player smoke checks

Use the same build and playlist on the affected platforms: Android phone, Xiaomi
Android TV 14 with its remote, and Avalonia desktop (mouse/keyboard or gamepad).
The full Android remote/installation checks remain in
[android-remote-smoke.md](android-remote-smoke.md); TV acceptance is tracked in #73.

## Quick pass before merging a player refactor

| Step | Action | Expected result |
| --- | --- | --- |
| First opening | Launch the app, open a group and play a known working channel. Also try a provider channel. | Correct channel title, actual video and audio; loading finishes. Fullscreen layout and controls remain usable. |
| Fast switching | Switch at least five channels quickly, including before the first frame. | Only the final selection remains active; title and EPG belong to it. Old replies cannot restore a previous channel or error. |
| Picker | Open the channel picker, browse, select another channel and reopen it. | Browsing/focus movement does not change playback; selection does. Current channel is highlighted and visible. |
| EPG and overlays | Open EPG, browse programs, then switch to the picker. Wait over four seconds with each panel open; close it. | EPG navigation does not change channel. One overlay at a time; open panels stay visible. Bars auto-hide about four seconds after closing the overlay. Missing EPG does not prevent video. |
| Background/return | While playing, use Home/Recents or another app for ten seconds, then return. Repeat while connecting and with a panel open. On the phone also lock/unlock. | Android audio/streaming stops in the background. Returning makes a fresh live connection with both video and audio, without a black screen or duplicate playback. Controls and focus still work. |
| Exit/re-entry | Leave the player and reopen it five times, including while connecting. | Leaving stops playback. Delayed URL, guide and surface callbacks do not reopen it or change the new player. No duplicate selection/focus actions. |
| Failure/recovery | With a controlled unavailable channel, wait for failure, then select a working channel. | Loading ends with a useful error for the selected channel; a working channel starts normally. No old-channel restart on background/return. |

For desktop backgrounding, preserve the desktop platform's existing behavior;
the Android stop/reconnect policy is not a requirement to change desktop behavior.
Use known working test channels so provider outages can be distinguished from a
player regression. Do not include stream credentials or signed URLs in reports.

## Record results in the PR

```text
Build/tag and commit:
Device/model, OS version, CPU ABI (Android):
Input: touch / remote / keyboard / mouse / gamepad
Source: M3U / provider name (no credentials)
First opening / fast switching / picker / EPG:
Background-return / exit-re-entry / failure-recovery:
Result: PASS / FAIL / NOT TESTED for each applicable step
Failure: exact steps, expected/actual behavior, audio present?, bars visible?, logs
```

Automated unit tests, successful builds and APK ABI/manifest inspection do not
mark a physical-device result as PASS. If a device is unavailable, record NOT
TESTED and keep its validation item open rather than claiming platform support.
