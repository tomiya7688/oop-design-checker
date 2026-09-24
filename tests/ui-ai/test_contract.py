#!/usr/bin/env python3
import importlib.util
import json
import tempfile
import unittest
from pathlib import Path

ROOT = Path(__file__).resolve().parent


def load_module(name, path):
    spec = importlib.util.spec_from_file_location(name, path)
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module


aggregate_module = load_module("aggregate_module", ROOT / "aggregate.py")
provider_module = load_module("provider_module", ROOT / "run_provider.py")


class AggregateTests(unittest.TestCase):
    def result(self, provider, status="ok", verdict="pass"):
        return {
            "provider": provider,
            "model": "test",
            "status": status,
            "verdict": None if status != "ok" else {
                "scenario": "release-candidate-ui",
                "verdict": verdict,
                "checks": [],
                "blockingIssues": [],
                "summary": "test",
            },
            "error": None if status == "ok" else "error",
        }

    def test_three_pass(self):
        value = aggregate_module.aggregate([
            self.result("openai"),
            self.result("gemini"),
            self.result("anthropic"),
        ])
        self.assertEqual("pass", value["verdict"])

    def test_two_fail_blocks(self):
        value = aggregate_module.aggregate([
            self.result("openai", verdict="fail"),
            self.result("gemini", verdict="fail"),
            self.result("anthropic"),
        ])
        self.assertEqual("fail", value["verdict"])

    def test_one_fail_requires_review(self):
        value = aggregate_module.aggregate([
            self.result("openai", verdict="fail"),
            self.result("gemini"),
            self.result("anthropic"),
        ])
        self.assertEqual("review_required", value["verdict"])

    def test_one_provider_error_requires_review(self):
        value = aggregate_module.aggregate([
            self.result("openai", status="error"),
            self.result("gemini"),
            self.result("anthropic"),
        ])
        self.assertEqual("review_required", value["verdict"])

    def test_two_provider_errors_block(self):
        value = aggregate_module.aggregate([
            self.result("openai", status="error"),
            self.result("gemini", status="error"),
            self.result("anthropic"),
        ])
        self.assertEqual("fail", value["verdict"])


class VerdictValidationTests(unittest.TestCase):
    def test_valid_verdict(self):
        checklist = json.loads((ROOT / "checklist.json").read_text(encoding="utf-8"))
        verdict = {
            "scenario": "release-candidate-ui",
            "verdict": "pass",
            "checks": [
                {"id": item["id"], "verdict": "pass", "evidence": "evidence"}
                for item in checklist["checks"]
            ],
            "blockingIssues": [],
            "summary": "all checks passed",
        }
        provider_module.validate_verdict(verdict, checklist)

    def test_missing_check_rejected(self):
        checklist = json.loads((ROOT / "checklist.json").read_text(encoding="utf-8"))
        verdict = {
            "scenario": "release-candidate-ui",
            "verdict": "pass",
            "checks": [],
            "blockingIssues": [],
            "summary": "invalid",
        }
        with self.assertRaises(ValueError):
            provider_module.validate_verdict(verdict, checklist)


if __name__ == "__main__":
    unittest.main(verbosity=2)
