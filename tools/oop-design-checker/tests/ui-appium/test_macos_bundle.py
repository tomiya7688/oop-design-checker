import os
import plistlib
import stat
import tempfile
import unittest
from pathlib import Path

from macos_bundle import BUNDLE_ID, EXECUTABLE, REQUIRED_FILES, create_bundle, validate_bundle


class MacOsBundleTests(unittest.TestCase):
    def setUp(self):
        self.temporary = tempfile.TemporaryDirectory()
        self.addCleanup(self.temporary.cleanup)
        self.root = Path(self.temporary.name)
        self.publish = self.root / "publish with spaces"
        for name in REQUIRED_FILES:
            path = self.publish / name
            path.parent.mkdir(parents=True, exist_ok=True)
            path.write_bytes(name.encode("utf-8"))
        (self.publish / EXECUTABLE).chmod(0o755)
        self.bundle = self.root / "OOP Design Checker.app"

    def test_bundle_metadata_and_all_publish_files_are_preserved(self):
        resource = self.publish / "ja" / "resources.dll"
        resource.parent.mkdir()
        resource.write_bytes(b"localized resources")
        (self.publish / ".publish-marker").write_bytes(b"hidden file")
        create_bundle(self.publish, self.bundle, "0.1.0")
        self.assertEqual(BUNDLE_ID, validate_bundle(self.bundle))
        with (self.bundle / "Contents" / "Info.plist").open("rb") as stream:
            metadata = plistlib.load(stream)
        self.assertEqual(EXECUTABLE, metadata["CFBundleExecutable"])
        self.assertEqual("APPL", metadata["CFBundlePackageType"])
        self.assertEqual("0.1.0", metadata["CFBundleShortVersionString"])
        self.assertEqual("0.1.0", metadata["CFBundleVersion"])
        for path in self.publish.rglob("*"):
            if path.is_file():
                copied = self.bundle / "Contents" / "MacOS" / path.relative_to(self.publish)
                self.assertEqual(path.read_bytes(), copied.read_bytes())
                if os.name != "nt":
                    self.assertEqual(stat.S_IMODE(path.stat().st_mode), stat.S_IMODE(copied.stat().st_mode))

    @unittest.skipIf(os.name == "nt", "Unix bundle symlink semantics")
    def test_relative_symlink_is_preserved(self):
        (self.publish / "apphost-link").symlink_to(EXECUTABLE)
        create_bundle(self.publish, self.bundle, "1.0.0")
        copied = self.bundle / "Contents" / "MacOS" / "apphost-link"
        self.assertTrue(copied.is_symlink())
        self.assertEqual(EXECUTABLE, os.readlink(copied))

    def test_bare_apphost_is_rejected_before_appium(self):
        with self.assertRaisesRegex(ValueError, "not a bare executable"):
            validate_bundle(self.publish / EXECUTABLE)

    def test_missing_support_files_are_rejected(self):
        missing = self.publish / "ReferenceAssemblies" / "System.Runtime.dll"
        missing.unlink()
        with self.assertRaisesRegex(ValueError, "System.Runtime.dll"):
            create_bundle(self.publish, self.bundle, "1.0.0")
        self.assertFalse(self.bundle.exists())

    def test_existing_destination_is_not_overwritten(self):
        self.bundle.mkdir()
        marker = self.bundle / "keep.txt"
        marker.write_text("keep", encoding="utf-8")
        with self.assertRaises(FileExistsError):
            create_bundle(self.publish, self.bundle, "1.0.0")
        self.assertEqual("keep", marker.read_text(encoding="utf-8"))

    def test_invalid_destination_or_version_is_rejected(self):
        for destination, version in (
            (self.publish / "recursive.app", "1.0.0"),
            (self.root / "no-app-suffix", "1.0.0"),
            (self.bundle, "unknown"),
        ):
            with self.subTest(destination=destination, version=version):
                with self.assertRaises(ValueError):
                    create_bundle(self.publish, destination, version)
                self.assertFalse(destination.exists())

    def test_incorrect_bundle_executable_is_rejected(self):
        create_bundle(self.publish, self.bundle, "1.0.0")
        plist = self.bundle / "Contents" / "Info.plist"
        with plist.open("rb") as stream:
            metadata = plistlib.load(stream)
        metadata["CFBundleExecutable"] = "../wrong-app"
        with plist.open("wb") as stream:
            plistlib.dump(metadata, stream)
        with self.assertRaisesRegex(ValueError, "application metadata"):
            validate_bundle(self.bundle)


if __name__ == "__main__":
    unittest.main()
