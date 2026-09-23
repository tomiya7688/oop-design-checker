import importlib.util
import json
import os
import tempfile
import unittest
from pathlib import Path

ROOT = Path(__file__).resolve().parent
SPEC = importlib.util.spec_from_file_location("judge", ROOT / "judge.py")
judge = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(judge)


def valid_verdict(scenario="fixture-analysis"):
    return {
        "scenario": scenario,
        "verdict": "pass",
        "checks": [
            {"id": f"UI-{index:03d}", "verdict": "pass", "evidence": "evidence"}
            for index in range(1, 13)
        ],
        "blockingIssues": [],
        "summary": "summary",
    }


class JudgeContractTests(unittest.TestCase):
    def test_schema_accepts_complete_verdict(self):
        schema = judge.read_json(ROOT / "verdict.schema.json")
        judge.validate_verdict(valid_verdict(), schema)

    def test_duplicate_check_id_is_rejected(self):
        schema = judge.read_json(ROOT / "verdict.schema.json")
        document = valid_verdict()
        document["checks"][-1]["id"] = "UI-001"
        with self.assertRaises(ValueError):
            judge.validate_verdict(document, schema)

    def test_missing_secret_becomes_provider_error_result(self):
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            scenario = root / "fixture-analysis"
            scenario.mkdir()
            (scenario / "expected.json").write_text("{}", encoding="utf-8")
            output = root / "openai.json"

            previous = os.environ.pop("OPENAI_API_KEY", None)
            previous_argv = __import__("sys").argv
            try:
                __import__("sys").argv = [
                    "judge.py",
                    "--provider",
                    "openai",
                    "--scenario-dir",
                    str(scenario),
                    "--output",
                    str(output),
                ]
                self.assertEqual(0, judge.main())
            finally:
                __import__("sys").argv = previous_argv
                if previous is not None:
                    os.environ["OPENAI_API_KEY"] = previous

            result = json.loads(output.read_text(encoding="utf-8"))
            self.assertEqual("error", result["status"])
            self.assertIsNone(result["result"])
            self.assertIn("OPENAI_API_KEY", result["error"])

    def test_prompt_includes_scenario_and_evidence(self):
        with tempfile.TemporaryDirectory() as temporary:
            scenario = Path(temporary) / "01-cancel"
            scenario.mkdir()
            (scenario / "expected.json").write_text('{"status":"Analysis cancelled."}', encoding="utf-8")
            prompt = judge.build_prompt(scenario)
            self.assertIn("<scenario>01-cancel</scenario>", prompt)
            self.assertIn("Analysis cancelled.", prompt)


if __name__ == "__main__":
    unittest.main()
