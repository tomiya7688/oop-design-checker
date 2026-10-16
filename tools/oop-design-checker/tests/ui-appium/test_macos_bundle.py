import os
import plistlib
import stat
import tempfile
import unittest
from pathlib import Path

from macos_bundle import アプリ識別子, 実行ファイル名, 必須ファイル一覧, アプリバンドルを作る, アプリバンドルを検証する


class macOSバンドルテスト(unittest.TestCase):
    def setUp(self):
        self.temporary = tempfile.TemporaryDirectory()
        self.addCleanup(self.temporary.cleanup)
        self.root = Path(self.temporary.name)
        self.publish = self.root / "publish with spaces"
        for name in 必須ファイル一覧:
            path = self.publish / name
            path.parent.mkdir(parents=True, exist_ok=True)
            path.write_bytes(name.encode("utf-8"))
        (self.publish / 実行ファイル名).chmod(0o755)
        self.bundle = self.root / "OOP Design Checker.app"

    def test_バンドル情報と全配布ファイルを維持する(self):
        resource = self.publish / "ja" / "resources.dll"
        resource.parent.mkdir()
        resource.write_bytes(b"localized resources")
        (self.publish / ".publish-marker").write_bytes(b"hidden file")
        アプリバンドルを作る(self.publish, self.bundle, "0.1.0")
        self.assertEqual(アプリ識別子, アプリバンドルを検証する(self.bundle))
        with (self.bundle / "Contents" / "Info.plist").open("rb") as stream:
            metadata = plistlib.load(stream)
        self.assertEqual(実行ファイル名, metadata["CFBundleExecutable"])
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
    def test_相対シンボリックリンクを維持する(self):
        (self.publish / "apphost-link").symlink_to(実行ファイル名)
        アプリバンドルを作る(self.publish, self.bundle, "1.0.0")
        copied = self.bundle / "Contents" / "MacOS" / "apphost-link"
        self.assertTrue(copied.is_symlink())
        self.assertEqual(実行ファイル名, os.readlink(copied))

    def test_単体apphostをAppium開始前に拒否する(self):
        with self.assertRaisesRegex(ValueError, "not a bare executable"):
            アプリバンドルを検証する(self.publish / 実行ファイル名)

    def test_必須の補助ファイルがない場合に拒否する(self):
        missing = self.publish / "ReferenceAssemblies" / "System.Runtime.dll"
        missing.unlink()
        with self.assertRaisesRegex(ValueError, "System.Runtime.dll"):
            アプリバンドルを作る(self.publish, self.bundle, "1.0.0")
        self.assertFalse(self.bundle.exists())

    def test_既存の出力先を上書きしない(self):
        self.bundle.mkdir()
        marker = self.bundle / "keep.txt"
        marker.write_text("keep", encoding="utf-8")
        with self.assertRaises(FileExistsError):
            アプリバンドルを作る(self.publish, self.bundle, "1.0.0")
        self.assertEqual("keep", marker.read_text(encoding="utf-8"))

    def test_不正な出力先またはバージョンを拒否する(self):
        for destination, version in (
            (self.publish / "recursive.app", "1.0.0"),
            (self.root / "no-app-suffix", "1.0.0"),
            (self.bundle, "unknown"),
        ):
            with self.subTest(destination=destination, version=version):
                with self.assertRaises(ValueError):
                    アプリバンドルを作る(self.publish, destination, version)
                self.assertFalse(destination.exists())

    def test_不正なバンドル実行ファイルを拒否する(self):
        アプリバンドルを作る(self.publish, self.bundle, "1.0.0")
        plist = self.bundle / "Contents" / "Info.plist"
        with plist.open("rb") as stream:
            metadata = plistlib.load(stream)
        metadata["CFBundleExecutable"] = "../wrong-app"
        with plist.open("wb") as stream:
            plistlib.dump(metadata, stream)
        with self.assertRaisesRegex(ValueError, "application metadata"):
            アプリバンドルを検証する(self.bundle)


if __name__ == "__main__":
    unittest.main()
