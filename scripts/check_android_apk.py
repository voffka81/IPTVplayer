"""Reject APKs that omit Android TV ARMv7 support or require touch input."""
import argparse
import os
import re
import subprocess
import zipfile
from pathlib import Path

REQUIRED_ABIS = ('armeabi-v7a', 'arm64-v8a', 'x86_64')
REQUIRED_LIBRARIES = ('libmonodroid.so', 'libmonosgen-2.0.so', 'libvlc.so', 'libc++_shared.so')


def validate_reports(files, badging, manifest):
    for abi in REQUIRED_ABIS:
        for library in REQUIRED_LIBRARIES:
            if f'lib/{abi}/{library}' not in files:
                raise ValueError(f'Missing {abi}/{library}: APK cannot support the expected TV/phone ABI')
    required_features = re.findall(r"^\s*uses-feature:\s+name='([^']+)'", badging, re.MULTILINE)
    for feature in required_features:
        if feature.startswith(('android.hardware.touchscreen', 'android.hardware.faketouch')):
            raise ValueError(f'TV-incompatible required touch feature: {feature}')
    minimum = re.search(r"(?:minSdkVersion|sdkVersion):'(\d+)'", badging)
    if not minimum or int(minimum[1]) > 34:
        raise ValueError('APK minimum SDK must allow Android TV 14 (API 34)')
    if 'android.intent.category.LEANBACK_LAUNCHER' not in manifest:
        raise ValueError('APK has no Android TV launcher entry')
    if 'android:banner' not in manifest:
        raise ValueError('APK has no Android TV banner')


def find_aapt2():
    sdk = os.environ.get('ANDROID_HOME') or os.environ.get('ANDROID_SDK_ROOT')
    candidates = list(Path(sdk).glob('build-tools/*/aapt2')) if sdk else []
    if not candidates:
        raise ValueError('Set ANDROID_HOME or pass --aapt2 to inspect the compiled manifest')
    return max(candidates, key=lambda path: tuple(int(part) for part in re.findall(r'\d+', path.parent.name)))


def check_apk(apk, aapt2):
    with zipfile.ZipFile(apk) as archive:
        files = set(archive.namelist())
    badging = subprocess.check_output([str(aapt2), 'dump', 'badging', str(apk)], text=True)
    manifest = subprocess.check_output([str(aapt2), 'dump', 'xmltree', '--file', 'AndroidManifest.xml', str(apk)], text=True)
    validate_reports(files, badging, manifest)
    print(f'APK compatibility OK: ARMv7, ARM64, x64 runtimes + VLC; optional touch; TV launcher/banner; min SDK <= 34 ({apk})')


if __name__ == '__main__':
    parser = argparse.ArgumentParser()
    parser.add_argument('apk', type=Path)
    parser.add_argument('--aapt2', type=Path)
    args = parser.parse_args()
    check_apk(args.apk, args.aapt2 or find_aapt2())
