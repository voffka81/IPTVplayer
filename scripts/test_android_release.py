import json
import tempfile
import unittest
from pathlib import Path
from android_release import next_version, version_code, write_manifest


class AndroidReleaseTests(unittest.TestCase):
    def test_versions_include_dev_and_legacy_tags(self):
        self.assertEqual('2.1.0', next_version(['2.0.1', '2.0.0'], ['v2.0.8-dev.123', 'android-v2.0.7'], 'minor'))
        self.assertEqual('2.0.9', next_version(['2.0.1'], ['v2.0.8-dev.123'], 'patch'))

    def test_codes_never_wrap_or_downgrade_across_channels(self):
        self.assertEqual(1900000001, version_code([1800000000, 1900000000], now=1790000000))
        self.assertEqual(1900000000, version_code([1800000000], now=1900000000))
        with self.assertRaises(ValueError):
            version_code([2100000000], now=1900000000)

    def test_manifest_matches_exact_apk(self):
        with tempfile.TemporaryDirectory() as directory:
            apk = Path(directory) / 'IPTVplayer.apk'
            apk.write_bytes(b'abc')
            write_manifest(apk, '2.0.2-dev.123', 123)
            data = json.loads(apk.with_name('android-update.json').read_text())
            self.assertEqual(123, data['versionCode'])
            self.assertEqual(3, data['size'])
            self.assertEqual('ba7816bf8f01cfea414140de5dae2223b00361a396177a9cb410ff61f20015ad', data['sha256'])
            self.assertEqual(apk.name, data['apkAssetName'])


if __name__ == '__main__':
    unittest.main()
