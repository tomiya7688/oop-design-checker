import json
import subprocess
import sys
import tempfile
import unittest
from pathlib import Path

ROOT = Path(__file__).resolve().parent
AGGREGATE = ROOT / "aggregate.py"
CHECK_IDS = [f"UI-{index:03d}" for index in range(1, 13)]


def provider_result(provider, verdict="pass", check_verdict="pass"):
    return {
        "provider": provider,
        "model": f"{provider}-model",
        "status": "ok",
        "error": None,
        "result": {
            "scenario": "fixture-analysis",
            "verdict": verdict,
            "checks": [
                {"id": check_id, "verdict": check_verdict, "evidence": "evidence"}
                for check_id in CHECK_IDS
            ],
            "blockingIssues": [],
            "summary": "summary",
        },
    }


class AggregateTests(unittest.TestCase):
    def run_case(self, providers, waiver=None):
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            results = root / "results"
            results.mkdir()
            for provider, data in providers.items():
                (results / f"{provider}.json").write_text(
                    json.dumps(data), encoding="utf-8"
                )
            output = root / "aggregate.json"
            command = [
                sys.executable,
                str(AGGREGATE),
                "--results",
                str(results),
                "--output",
                str(output),
            ]
            if waiver is not None:
                waiver_path = root / "waiver.json"
                waiver_path.write_text(json.dumps(waiver), encoding="utf-8")
                command.extend(["--waiver", str(waiver_path)])
            completed = subprocess.run(command, capture_output=True, text=True, check=False)
            document = json.loads(output.read_text(encoding="utf-8")) if output.exists() else None
            return completed.returncode, document

    def test_all_pass(self):
        code, result = self.run_case(
            {provider: provider_result(provider) for provider in ("openai", "google", "anthropic")}
        )
        self.assertEqual(0, code)
        self.assertEqual("pass", result["verdict"])
        self.assertEqual("pass", result["finalVerdict"])

    def test_two_provider_failures_fail_gate(self):
        providers = {
            "openai": provider_result("openai", verdict="fail", check_verdict="fail"),
            "google": provider_result("google", verdict="fail", check_verdict="fail"),
            "anthropic": provider_result("anthropic"),
        }
        code, result = self.run_case(providers)
        self.assertEqual(1, code)
        self.assertEqual("fail", result["verdict"])

    def test_one_review_requires_review(self):
        providers = {
            "openai": provider_result("openai"),
            "google": provider_result("google", verdict="review", check_verdict="review"),
            "anthropic": provider_result("anthropic"),
        }
        code, result = self.run_case(providers)
        self.assertEqual(1, code)
        self.assertEqual("review_required", result["verdict"])

    def test_one_missing_provider_requires_review(self):
        code, result = self.run_case(
            {
                "openai": provider_result("openai"),
                "google": provider_result("google"),
            }
        )
        self.assertEqual(1, code)
        self.assertEqual("review_required", result["verdict"])

    def test_two_missing_providers_fail(self):
        code, result = self.run_case({"openai": provider_result("openai")})
        self.assertEqual(1, code)
        self.assertEqual("fail", result["verdict"])

    def test_review_can_be_explicitly_waived(self):
        providers = {
            "openai": provider_result("openai"),
            "google": provider_result("google", verdict="review", check_verdict="review"),
            "anthropic": provider_result("anthropic"),
        }
        code, result = self.run_case(
            providers,
            waiver={
                "scenario": "fixture-analysis",
                "reason": "Reviewed evidence manually.",
                "approvedBy": "release-owner",
                "reference": "issue-123",
            },
        )
        self.assertEqual(0, code)
        self.assertEqual("review_required", result["verdict"])
        self.assertEqual("pass_with_waiver", result["finalVerdict"])

    def test_fail_cannot_be_waived(self):
        providers = {
            "openai": provider_result("openai", verdict="fail", check_verdict="fail"),
            "google": provider_result("google", verdict="fail", check_verdict="fail"),
            "anthropic": provider_result("anthropic"),
        }
        code, result = self.run_case(
            providers,
            waiver={
                "scenario": "fixture-analysis",
                "reason": "Not allowed.",
                "approvedBy": "release-owner",
            },
        )
        self.assertNotEqual(0, code)
        self.assertIsNone(result)


if __name__ == "__main__":
    unittest.main()
