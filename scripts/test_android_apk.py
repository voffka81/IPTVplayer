import re
import unittest
from check_android_apk import REQUIRED_ABIS, REQUIRED_LIBRARIES, validate_reports


class AndroidApkTests(unittest.TestCase):
    def setUp(self):
        self.files = {f'lib/{abi}/{library}' for abi in REQUIRED_ABIS for library in REQUIRED_LIBRARIES}
        self.badging = "minSdkVersion:'21'\nuses-feature-not-required: name='android.hardware.touchscreen'\n"
        self.manifest = 'android.intent.category.LEANBACK_LAUNCHER android:banner=@0x123'

    def test_universal_apk_with_optional_touch_supports_tv(self):
        validate_reports(self.files, self.badging, self.manifest)

    def test_old_64_bit_only_package_is_rejected(self):
        files = {file for file in self.files if '/armeabi-v7a/' not in file}
        with self.assertRaisesRegex(ValueError, 'armeabi-v7a'):
            validate_reports(files, self.badging, self.manifest)

    def test_vlc_and_managed_runtime_must_both_be_present_for_every_abi(self):
        for abi in REQUIRED_ABIS:
            for library in REQUIRED_LIBRARIES:
                with self.subTest(abi=abi, library=library):
                    with self.assertRaisesRegex(ValueError, re.escape(library)):
                        validate_reports(self.files - {f'lib/{abi}/{library}'}, self.badging, self.manifest)

    def test_required_real_or_fake_touch_is_rejected(self):
        for feature in ('touchscreen', 'faketouch', 'touchscreen.multitouch'):
            with self.subTest(feature=feature):
                with self.assertRaisesRegex(ValueError, 'required touch'):
                    validate_reports(self.files, self.badging + f"uses-feature: name='android.hardware.{feature}'\n", self.manifest)

    def test_api_34_must_be_supported(self):
        with self.assertRaisesRegex(ValueError, 'minimum SDK'):
            validate_reports(self.files, "minSdkVersion:'35'", self.manifest)

    def test_tv_entry_and_banner_must_survive_manifest_merge(self):
        for manifest in ('android:banner=@123', 'android.intent.category.LEANBACK_LAUNCHER'):
            with self.subTest(manifest=manifest):
                with self.assertRaises(ValueError):
                    validate_reports(self.files, self.badging, manifest)


if __name__ == '__main__':
    unittest.main()
