# Android APK updates

Android installs an APK as an update only when its application ID and signing
certificate match the installed app, and its version code is not lower. The
application ID is `com.voffka81.iptvplayer`.

The Android CI workflow supports a persistent release keystore. Create one once
and keep a secure backup; losing it prevents publishing updates over installs
signed with that key. Add these repository Actions secrets:

- `ANDROID_KEYSTORE_BASE64`: Base64-encoded keystore file
- `ANDROID_KEYSTORE_PASSWORD`: keystore password
- `ANDROID_KEY_ALIAS`: key alias
- `ANDROID_KEY_PASSWORD`: key password

For example, create a keystore with `keytool -genkeypair -v -keystore
iptv-release.keystore -alias iptvplayer -keyalg RSA -keysize 2048 -validity
10000`. On PowerShell, encode it with
`[Convert]::ToBase64String([IO.File]::ReadAllBytes('iptv-release.keystore'))`;
add that output and the passwords/alias as GitHub repository secrets under
Settings → Secrets and variables → Actions. Never commit the keystore or its
passwords.

CI assigns each APK a larger Android version code from the GitHub Actions run
number. `ApplicationDisplayVersion` in `TV Player MAUI.csproj` is the version
shown to users. Local builds use the project `ApplicationVersion` value.

The APK that was already installed before a persistent keystore was configured
cannot be updated with the new key. Remove that test install once, then install
the APK produced after the secrets are configured. Later APKs will update it.
