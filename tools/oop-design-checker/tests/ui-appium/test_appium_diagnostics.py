import json
import os
import unittest
from pathlib import Path

from appium_diagnostics import (
    build_failure_report,
    build_wait_context,
    capture_evidence,
    format_failure_report,
    write_failure_report,
)


class AppiumDiagnosticsTests(unittest.TestCase):
    def test_empty_exception_message_still_reports_type_and_traceback(self):
        error = TimeoutError("")
        wait_context = build_wait_context(
            {
                "scenario": "09-keyboard-menu",
                "lastSuccessfulAction": "visible-shortcut-hints",
            },
            "SearchFilter has keyboard focus",
            "SearchFilter",
            20,
            "run_appium.py:537",
        )
        report = build_failure_report(
            error,
            "Traceback (most recent call last):\n  TimeoutError\n",
            context=wait_context,
        )

        formatted = format_failure_report(report)
        self.assertTrue(report["messageIsEmpty"])
        self.assertIn("Exception: TimeoutError: (empty message)", formatted)
        self.assertIn("Scenario: 09-keyboard-menu", formatted)
        self.assertIn("Target element: SearchFilter", formatted)
        self.assertIn("Timeout: 20 seconds", formatted)
        self.assertIn("Source: run_appium.py:537", formatted)
        self.assertIn("Traceback (most recent call last)", formatted)
        self.assertEqual("visible-shortcut-hints", report["lastSuccessfulAction"])

    def test_failure_report_writes_json_with_context_and_evidence_paths(self):
        report = build_failure_report(
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
            write_failure_report(path, report)
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

    def test_evidence_capture_error_does_not_replace_primary_exception(self):
        primary = RuntimeError("primary UI failure")

        def fail_to_capture():
            raise PermissionError("closed session")

        evidence_error = capture_evidence("page source", fail_to_capture)
        report = build_failure_report(
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
