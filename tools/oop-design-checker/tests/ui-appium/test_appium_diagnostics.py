import json
import os
import unittest
from pathlib import Path

from appium_diagnostics import (
    失敗報告を組み立てる,
    待機状況を組み立てる,
    証跡を取得する,
    失敗報告を書式化する,
    失敗報告を書き出す,
)


class Appium診断テスト(unittest.TestCase):
    def test_例外メッセージが空でも型と追跡情報を報告する(self):
        error = TimeoutError("")
        wait_context = 待機状況を組み立てる(
            {
                "scenario": "09-keyboard-menu",
                "lastSuccessfulAction": "visible-shortcut-hints",
            },
            "SearchFilter has keyboard focus",
            "SearchFilter",
            20,
            "run_appium.py:537",
        )
        report = 失敗報告を組み立てる(
            error,
            "Traceback (most recent call last):\n  TimeoutError\n",
            context=wait_context,
        )

        formatted = 失敗報告を書式化する(report)
        self.assertTrue(report["messageIsEmpty"])
        self.assertIn("Exception: TimeoutError: (empty message)", formatted)
        self.assertIn("Scenario: 09-keyboard-menu", formatted)
        self.assertIn("Target element: SearchFilter", formatted)
        self.assertIn("Timeout: 20 seconds", formatted)
        self.assertIn("Source: run_appium.py:537", formatted)
        self.assertIn("Traceback (most recent call last)", formatted)
        self.assertEqual("visible-shortcut-hints", report["lastSuccessfulAction"])

    def test_失敗報告へ状況と証跡のパスをJSONで書き出す(self):
        report = 失敗報告を組み立てる(
            RuntimeError("driver disconnected"),
            "RuntimeError: driver disconnected",
            context={
                "scenario": "02-fixture-analysis",
                "action": "wait until: diagnostics summary updates",
                "expectedCondition": "summary matches expected counts",
                "automationId": "DangerCount",
                "timeoutSeconds": 120,
                "lastSuccessfulAction": "set-target",
            },
            evidence_paths={
                "screenshot": "failure.png",
                "pageSource": "page-source.xml",
            },
            evidence_errors=["screenshot: session already closed"],
        )

        repository_root = Path(__file__).resolve().parents[4]
        path = repository_root / f".appium-failure-test-{os.getpid()}.json"
        self.assertEqual(repository_root, path.resolve().parent)
        try:
            失敗報告を書き出す(path, report)
            saved = json.loads(path.read_text(encoding="utf-8"))
        finally:
            path.unlink(missing_ok=True)

        self.assertEqual("RuntimeError", saved["exceptionType"])
        self.assertEqual("02-fixture-analysis", saved["scenario"])
        self.assertEqual("DangerCount", saved["automationId"])
        self.assertEqual(120, saved["timeoutSeconds"])
        self.assertEqual("failure.png", saved["evidence"]["screenshot"])
        self.assertEqual(
            ["screenshot: session already closed"], saved["evidenceErrors"]
        )

    def test_証跡取得の失敗で元の例外を置き換えない(self):
        primary = RuntimeError("primary UI failure")

        def 証跡取得に失敗する():
            raise PermissionError("closed session")

        evidence_error = 証跡を取得する("page source", 証跡取得に失敗する)
        report = 失敗報告を組み立てる(
            primary,
            "RuntimeError: primary UI failure",
            evidence_errors=[evidence_error],
        )

        self.assertEqual("RuntimeError", report["exceptionType"])
        self.assertEqual("primary UI failure", report["message"])
        self.assertEqual(
            ["page source: PermissionError: closed session"],
            report["evidenceErrors"],
        )


if __name__ == "__main__":
    unittest.main()
