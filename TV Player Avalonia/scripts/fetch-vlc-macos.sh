#!/bin/sh
set -eu

project_dir="$1"
lib_dir="$project_dir/natives/macos/lib"
plugins_dir="$project_dir/natives/macos/plugins"

if [ -f "$lib_dir/libvlccore.dylib" ] && [ -f "$lib_dir/libvlc.dylib" ] && [ -d "$plugins_dir" ]; then
  echo "[VLC] macOS native libs and plugins already bundled"
  exit 0
fi

download_dir="$project_dir/obj/vlc-download"
mount_point="$download_dir/mount"
mkdir -p "$download_dir" "$project_dir/natives/macos"

arch="$(uname -m)"
case "$arch" in
  arm64|aarch64)
    dmg_name="vlc-3.0.21-arm64.dmg"
    ;;
  x86_64)
    dmg_name="vlc-3.0.21-intel64.dmg"
    ;;
  *)
    echo "[VLC] Unsupported macOS architecture: $arch"
    exit 1
    ;;
esac

url="https://get.videolan.org/vlc/3.0.21/macosx/$dmg_name"
dmg_path="$download_dir/$dmg_name"

if [ ! -f "$dmg_path" ]; then
  echo "[VLC] Downloading $url"
  curl -L --fail --retry 3 --retry-delay 2 -o "$dmg_path" "$url"
fi

if [ -d "$mount_point" ]; then
  hdiutil detach "$mount_point" >/dev/null 2>&1 || true
fi
mkdir -p "$mount_point"

cleanup() {
  hdiutil detach "$mount_point" >/dev/null 2>&1 || true
}
trap cleanup EXIT

echo "[VLC] Mounting DMG"
hdiutil attach "$dmg_path" -nobrowse -readonly -mountpoint "$mount_point" >/dev/null

if [ ! -d "$mount_point/VLC.app/Contents/MacOS/lib" ]; then
  echo "[VLC] Could not find VLC libs inside mounted image"
  exit 1
fi

echo "[VLC] Copying native libraries and plugins"
rm -rf "$project_dir/natives/macos/lib"
rm -rf "$project_dir/natives/macos/plugins"
cp -R "$mount_point/VLC.app/Contents/MacOS/lib" "$project_dir/natives/macos/"
if [ -d "$mount_point/VLC.app/Contents/MacOS/plugins" ]; then
  cp -R "$mount_point/VLC.app/Contents/MacOS/plugins" "$project_dir/natives/macos/"
fi

hdiutil detach "$mount_point" >/dev/null 2>&1 || true
trap - EXIT

echo "[VLC] Bundled macOS native runtime under $project_dir/natives/macos"