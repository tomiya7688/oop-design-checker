import hashlib
import json
import subprocess
import sys
import tempfile
import unittest
from unittest.mock import patch
from pathlib import Path

from inventory_appium import inventory_tree


class InventoryTests(unittest.TestCase):
    """{"責務": "依存物一覧の対象と失敗条件を検証します。"}"""

    def test_records_nested_packages_and_hashes_without_extra_metadata(self):
        """{"責務": "依存物とハッシュを記録し余分な情報を除外します。"}"""
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            package = root / "node_modules" / "example"
            package.mkdir(parents=True)
            (package / "package.json").write_text(
                json.dumps({"name": "example", "version": "1.2.3", "private": "omit"}),
                encoding="utf-8-sig",
            )
            (package / "binding.node").write_bytes(b"native")
            (package / "ignored.txt").write_text("ignore", encoding="utf-8")
            fixture = package / "test" / "fixture"
            fixture.mkdir(parents=True)
            (fixture / "package.json").write_text("{", encoding="utf-8")
            result = inventory_tree("drivers", root)
            self.assertEqual(
                [
                    {
                        "path": "node_modules/example/package.json",
                        "name": "example",
                        "version": "1.2.3",
                    }
                ],
                result["packages"],
            )
            self.assertEqual(
                [
                    {
                        "path": "node_modules/example/binding.node",
                        "bytes": 6,
                        "sha256": hashlib.sha256(b"native").hexdigest(),
                    }
                ],
                result["native_files"],
            )
            self.assertEqual([], result["skipped_links"])

    def test_missing_root_and_invalid_manifest_are_errors(self):
        """{"責務": "走査先の欠落と不正なマニフェストを拒否します。"}"""
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            with self.assertRaises(ValueError):
                inventory_tree("missing", root / "missing")
            (root / "package.json").write_text("{", encoding="utf-8")
            with self.assertRaises(json.JSONDecodeError):
                inventory_tree("invalid", root)

    def test_links_are_recorded_not_followed(self):
        """{"責務": "シンボリックリンクの先を走査しません。"}"""
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory) / "root"
            root.mkdir()
            target = Path(directory) / "outside"
            target.mkdir()
            (target / "secret.node").write_bytes(b"secret")
            try:
                (root / "link").symlink_to(target, target_is_directory=True)
            except OSError as error:
                self.skipTest(f"symbolic links unavailable: {error}")
            result = inventory_tree("root", root)
            self.assertEqual(["link"], result["skipped_links"])
            self.assertEqual([], result["native_files"])

    @unittest.skipUnless(hasattr(Path, "is_junction"), "junction detection requires Python 3.12+")
    def test_junction_is_recorded_without_traversal(self):
        """{"責務": "ジャンクションの先を走査しません。"}"""
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            junction = root / "junction"
            junction.mkdir()
            (junction / "hidden.node").write_bytes(b"outside")
            with patch.object(
                Path, "is_junction", autospec=True, side_effect=lambda path: path == junction
            ):
                result = inventory_tree("root", root)
            self.assertEqual(["junction"], result["skipped_links"])
            self.assertEqual([], result["native_files"])

    def test_cli_requires_expected_roots_before_writing_output(self):
        """{"責務": "対象のパッケージを確認してから結果を保存します。"}"""
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            appium = root / "appium"
            driver = root / "node_modules" / "driver"
            appium.mkdir()
            driver.mkdir(parents=True)
            (appium / "package.json").write_text(
                '{"name":"appium","version":"3.9.0"}', encoding="utf-8"
            )
            (driver / "package.json").write_text(
                '{"name":"driver","version":"1.0.0"}', encoding="utf-8"
            )
            nested = driver / "node_modules" / "missing"
            nested.mkdir(parents=True)
            (nested / "package.json").write_text(
                '{"name":"missing","version":"1.0.0"}', encoding="utf-8"
            )
            output = root / "report.json"
            command = [
                sys.executable,
                str(Path(__file__).with_name("inventory_appium.py")),
                "--appium",
                str(appium),
                "--drivers",
                str(root / "node_modules"),
                "--output",
                str(output),
                "--driver-package",
            ]
            failure = subprocess.run(command + ["missing"], capture_output=True, check=False)
            self.assertNotEqual(0, failure.returncode)
            self.assertFalse(output.exists())
            success = subprocess.run(command + ["driver"], capture_output=True, check=False)
            self.assertEqual(0, success.returncode, success.stderr)
            self.assertEqual(
                ["appium", "drivers"],
                [item["root"] for item in json.loads(output.read_text(encoding="utf-8"))["roots"]],
            )


if __name__ == "__main__":
    unittest.main()
