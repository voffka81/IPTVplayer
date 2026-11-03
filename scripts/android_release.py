"""Android release metadata and version policy used by the unified release workflow."""
import hashlib
import json
import re
import subprocess
import time
import urllib.request
from pathlib import Path

PACKAGE_ID = "com.voffka81.iptvplayer"
REPOSITORY = "voffka81/IPTVplayer"


def next_version(versions, tags, bump):
    versions = [tuple(map(int, value.split('.'))) for value in versions]
    for tag in tags:
        match = re.fullmatch(r"(?:v|android-v)?(\d+)\.(\d+)\.(\d+)(?:-dev\.\d+)?", tag)
        if match:
            versions.append(tuple(map(int, match.groups())))
    major, minor, patch = max(versions)
    if bump == 'major':
        return f'{major + 1}.0.0'
    if bump == 'minor':
        return f'{major}.{minor + 1}.0'
    if bump != 'patch':
        raise ValueError('Unknown version increment')
    return f'{major}.{minor}.{patch + 1}'


def version_code(previous_codes, now=None):
    # Shared high-water mark across stable and prerelease releases, never modulo.
    code = max(int(time.time() if now is None else now), max(previous_codes, default=0) + 1)
    if not 0 < code <= 2100000000:
        raise ValueError('Android versionCode limit reached')
    return code


def published_codes():
    pages = json.loads(subprocess.check_output([
        'gh', 'api', '--paginate', '--slurp', f'repos/{REPOSITORY}/releases?per_page=100'
    ], text=True))
    codes = []
    for page in pages:
        for release in page:
            if release['draft']:
                continue
            for asset in release['assets']:
                if asset['name'] != 'android-update.json':
                    continue
                url = asset['browser_download_url']
                if not url.startswith(f'https://github.com/{REPOSITORY}/releases/download/'):
                    raise ValueError('Unexpected metadata source')
                with urllib.request.urlopen(url, timeout=30) as response:
                    metadata = json.loads(response.read(65537))
                code = metadata['versionCode']
                if metadata['packageId'] != PACKAGE_ID or type(code) is not int or not 0 < code <= 2100000000:
                    raise ValueError('Invalid published version metadata')
                codes.append(code)
    return codes


def write_manifest(apk, version_name, code):
    apk = Path(apk)
    with apk.open('rb') as stream:
        checksum = hashlib.file_digest(stream, 'sha256').hexdigest()
    manifest = dict(schemaVersion=1, packageId=PACKAGE_ID, versionCode=int(code),
                    versionName=version_name, apkAssetName=apk.name,
                    sha256=checksum,
                    size=apk.stat().st_size, minSdk=23)
    apk.with_name('android-update.json').write_text(json.dumps(manifest, indent=2) + '\n')


if __name__ == '__main__':
    import argparse
    parser = argparse.ArgumentParser()
    parser.add_argument('apk')
    parser.add_argument('version_name')
    parser.add_argument('code', type=int)
    args = parser.parse_args()
    write_manifest(args.apk, args.version_name, args.code)
