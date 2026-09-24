import importlib.util
import shutil
import tempfile
import unittest
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
SCRIPT = ROOT / "tests" / "release" / "SpecificationContract.py"
SPEC = importlib.util.spec_from_file_location("specification_contract", SCRIPT)
contract = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(contract)


class SpecificationContractTests(unittest.TestCase):
    def test_repository_contract_passes(self):
        errors = contract.validate(ROOT)
        self.assertEqual([], errors, "\n".join(errors))

    def test_draft_heading_is_rejected(self):
        with tempfile.TemporaryDirectory() as temporary:
            copy_root = Path(temporary) / "repo"
            self._copy_contract_files(copy_root)
            path = copy_root / "specification" / "check-rules.md"
            text = path.read_text(encoding="utf-8")
            path.write_text(
                text.replace("# チェックルール", "# チェックルール（ドラフト）", 1),
                encoding="utf-8",
            )
            errors = contract.validate(copy_root)
            self.assertTrue(any("draft marker" in error for error in errors))

    def test_severity_table_drift_is_rejected(self):
        with tempfile.TemporaryDirectory() as temporary:
            copy_root = Path(temporary) / "repo"
            self._copy_contract_files(copy_root)
            path = copy_root / "specification" / "check-rules.en.md"
            text = path.read_text(encoding="utf-8")
            path.write_text(
                text.replace("| OOP404 | `ATTENTION` |", "| OOP404 | `WARNING` |", 1),
                encoding="utf-8",
            )
            errors = contract.validate(copy_root)
            self.assertTrue(any("severity table differs" in error for error in errors))

    def _copy_contract_files(self, destination):
        for relative in (
            "specification/object-oriented-design.md",
            "specification/object-oriented-design.en.md",
            "specification/check-rules.md",
            "specification/check-rules.en.md",
            "tests/fixtures/release-validation-csharp/expected-diagnostics.json",
        ):
            source = ROOT / relative
            target = destination / relative
            target.parent.mkdir(parents=True, exist_ok=True)
            shutil.copy2(source, target)

        rules_source = ROOT / "src" / "OopDesignChecker" / "Rules"
        rules_target = destination / "src" / "OopDesignChecker" / "Rules"
        rules_target.mkdir(parents=True, exist_ok=True)
        for source in rules_source.glob("*.cs"):
            shutil.copy2(source, rules_target / source.name)


if __name__ == "__main__":
    unittest.main()
