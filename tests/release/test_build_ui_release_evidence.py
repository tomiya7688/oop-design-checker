import json
import subprocess
import sys
import tempfile
import unittest
from pathlib import Path

ROOT = Path(__file__).resolve().parent
SCRIPT = ROOT / "BuildUiReleaseEvidence.py"
APPIUM = (
    "01-cancel",
    "02-fixture-analysis",
    "03-filter-detail",
    "04-language-resize",
    "05-config-editor",
    "06-export",
    "07-source-open-fallback",
    "08-error-state",
)
HEADLESS = (
    "ready-ja-small",
    "ready-ja-default",
    "ready-ja-large",
    "ready-en-default",
    "fixture-ja-default",
    "filter-detail-oop106-ja",
    "fixture-en-default",
    "error-ja-default",
    "config-editor-ja-default",
)


def write_json(path, value):
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(json.dumps(value), encoding="utf-8")


class BuildUiReleaseEvidenceTests(unittest.TestCase):
    def test_builds_normalized_release_candidate_bundle(self):
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            windows = root / "windows"
            macos = root / "macos"
            headless = root / "headless"
            output = root / "output"

            for platform_root in (windows, macos):
                for scenario in APPIUM:
                    directory = platform_root / "ui-appium-evidence" / scenario
                    write_json(directory / "expected.json", {"scenario": scenario})
                    write_json(directory / "ui-state.json", {"scenario": scenario})
                    write_json(directory / "action-log.json", [{"action": scenario}])
                    (directory / "after.png").write_bytes(b"png")
                    (directory / "resized.png").write_bytes(b"png")
                export = platform_root / "ui-appium-evidence" / "06-export"
                write_json(
                    export / "actual-diagnostics.json",
                    {"version": 1, "diagnostics": []},
                )
                write_json(
                    export / "actual-diagnostics.sarif",
                    {"version": "2.1.0", "runs": []},
                )

            for scenario in HEADLESS:
                directory = headless / scenario
                write_json(directory / "ui-state.json", {"scenario": scenario})
                (directory / "actual.png").write_bytes(b"png")

            completed = subprocess.run(
                [
                    sys.executable,
                    str(SCRIPT),
                    "--windows",
                    str(windows),
                    "--macos",
                    str(macos),
                    "--headless",
                    str(headless),
                    "--output",
                    str(output),
                    "--candidate-sha",
                    "a" * 40,
                ],
                capture_output=True,
                text=True,
                check=False,
            )
            self.assertEqual(0, completed.returncode, completed.stderr)
            candidate = output / "release-candidate"
            manifest = json.loads((candidate / "manifest.json").read_text())
            self.assertEqual("a" * 40, manifest["candidateSha"])
            self.assertEqual(10, len(manifest["visuals"]))
            self.assertTrue((candidate / "actual-diagnostics.json").exists())
            self.assertTrue((candidate / "windows-fixture.png").exists())
            self.assertTrue((candidate / "headless-config-editor.png").exists())

    def test_missing_required_scenario_fails(self):
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            for name in ("windows", "macos", "headless"):
                (root / name).mkdir()
            completed = subprocess.run(
                [
                    sys.executable,
                    str(SCRIPT),
                    "--windows",
                    str(root / "windows"),
                    "--macos",
                    str(root / "macos"),
                    "--headless",
                    str(root / "headless"),
                    "--output",
                    str(root / "output"),
                    "--candidate-sha",
                    "b" * 40,
                ],
                capture_output=True,
                text=True,
                check=False,
            )
            self.assertNotEqual(0, completed.returncode)


if __name__ == "__main__":
    unittest.main()
