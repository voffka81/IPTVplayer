# Android in-app updates

Open **Settings → App updates** (Настройки → Обновления приложения).
The default **Release** channel accepts stable published releases; **Dev** accepts
both stable releases and prereleases. The channel is saved in app preferences.
The playlist screen checks once per app process without delaying launch, showing
a button when a newer compatible update exists. Checks never download or install.

Press **Download**, then **Install update**. If Android requires permission to
install from this source, enable it in the system screen, return, and press
Install again. Android always asks the user to confirm installation. Cancelling
or leaving a download removes partial files. Network errors can be retried;
completed APKs remain in private cache for installation/retry and are cleaned
after a day at a subsequent startup. No playlists/settings are deleted.

On Android TV the channel picker and action buttons use native remote focus and
OK/Back behavior; buttons have a visible focus outline. A completed operation
moves focus to its next available action. Returning from Dev to Release never
installs an older or equal versionCode: wait until a newer stable release exists.
Google Play installations disable this updater and direct users to the store.

## Release contract

The unified manual `Publish unified release` workflow accepts **Release / Dev**
and the existing patch/minor/major choice. It builds Windows and Android with the
same display version, retaining the existing Android package ID and signing key.
Dev uses a `vX.Y.Z-dev.VERSIONCODE` tag and GitHub prerelease; Release uses a stable
`vX.Y.Z` tag. Only this workflow publishes update assets; PR artifacts are test-only.

Each release includes a signed universal Android APK and `android-update.json`:

```json
{
  "schemaVersion": 1,
  "packageId": "com.voffka81.iptvplayer",
  "versionCode": 1793700000,
  "versionName": "2.0.2",
  "apkAssetName": "IPTVplayer-android-2.0.2.apk",
  "sha256": "<64 hex characters>",
  "size": 75000000,
  "minSdk": 23
}
```

VersionCode is the greater of current Unix seconds and the highest code in
previously published stable/prerelease metadata plus one; it is checked against
Android's 2,100,000,000 limit. The shared release concurrency group serializes
both channels. There is no modulo wrap. Existing legacy signed APKs used run-ID
modulo codes; the first timestamp-coded release must still exceed the installed
code. The updater independently rejects downgrades, including after channel
changes. A release built from the same second still advances the high-water mark.

The app reads at most five 30-release API pages, ignores drafts and releases
without compatible metadata, and chooses the highest eligible versionCode. It
accepts only HTTPS asset URLs in the official voffka81/IPTVplayer release path,
limits response/download sizes, checks exact file size and SHA-256, then compares
APK package/version, signing certificates and native ABIs with the installed app.
PackageManager and the system installer enforce platform installation rules.
A download from a debug-signed app cannot install a release signed with a different
certificate. The private FileProvider grants read access only to the updates cache.

## Bootstrap and manual validation

An old app without this feature must be upgraded once by downloading the signed
APK manually. Publish subsequent releases using the updated workflow; older
releases without android-update.json are intentionally not offered. Publishing a
release is separate from merging this implementation.

Check on a signed phone install and Xiaomi Android TV 14:

1. Default Release, select Dev, restart and confirm persistence.
2. Check for an equal/older release (no update), a newer stable release, and a
   newer prerelease (Dev only). Switch Dev → Release without downgrading.
3. Download with progress; cancel midway, retry offline/online, and test insufficient
   storage. Leave/re-enter the page during checking and downloading.
4. Permit unknown-source installs if requested. Confirm the system installer opens
   through a content URI, then cancel and retry. Install over the existing app and
   verify package/signing identity, saved sources and preferences remain intact.
5. Use D-pad/OK/Back for channel choice, checking, download, cancel, permission
   settings and installer confirmation. Verify focus after background/resume.
6. Reject a corrupt/truncated APK, wrong package/version/signature and incompatible
   native ABI. Confirm Play-installed builds use their store update flow.

Automated .NET tests cover selection, trust boundaries, response limits, download
integrity and cancellation using local HTTP fakes. Python tests cover the release
version policy and metadata generation. Device installation/focus checks require
physical hardware; the application cannot silently install or bypass Android.
