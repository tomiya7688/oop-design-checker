import importlib.util
import tempfile
import unittest
from pathlib import Path

ROOT = Path(__file__).resolve().parent
SPEC = importlib.util.spec_from_file_location(
    "prerequisites",
    ROOT / "ValidateReleaseCandidatePrerequisites.py",
)
prerequisites = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(prerequisites)


class ReleaseCandidatePrerequisiteTests(unittest.TestCase):
    def populate(self, repository_root, *, version="1.0.0", draft=False):
        tool_root = repository_root / "tools" / "oop-design-checker"
        tool_root.mkdir(parents=True)
        (tool_root / "Directory.Build.props").write_text(
            f"<Project><PropertyGroup><VersionPrefix>{version}</VersionPrefix></PropertyGroup></Project>",
            encoding="utf-8",
        )
        (repository_root / "LICENSE").write_text("license\n", encoding="utf-8")
        (tool_root / "THIRD-PARTY-NOTICES.md").write_text("notices\n", encoding="utf-8")

        tool_docs = tool_root / "docs"
        tool_docs.mkdir()
        repository_docs = repository_root / "docs"
        repository_docs.mkdir()

        ja_suffix = "（ドラフト）" if draft else ""
        en_suffix = " (Draft)" if draft else ""
        (repository_docs / "object-oriented-design.md").write_text(
            f"# オブジェクト指向設計の定義{ja_suffix}\n", encoding="utf-8"
        )
        (tool_docs / "check-rules.md").write_text(
            f"# チェックルール{ja_suffix}\n", encoding="utf-8"
        )
        (repository_docs / "object-oriented-design.en.md").write_text(
            f"# Object-Oriented Design Definition{en_suffix}\n", encoding="utf-8"
        )
        (tool_docs / "check-rules.en.md").write_text(
            f"# Check Rules{en_suffix}\n", encoding="utf-8"
        )
        (tool_root / "CHANGELOG.md").write_text(
            "## 1.0.0 - 2026-09-24\n", encoding="utf-8"
        )
        (tool_root / "CHANGELOG.en.md").write_text(
            "## 1.0.0 - 2026-09-24\n", encoding="utf-8"
        )
        return tool_root

    def test_complete_1_0_0_prerequisites_pass(self):
        with tempfile.TemporaryDirectory() as temporary:
            repository_root = Path(temporary)
            tool_root = self.populate(repository_root)
            checks = prerequisites.check(tool_root, repository_root)
            self.assertTrue(all(item["passed"] for item in checks))

    def test_missing_release_inputs_are_reported_independently(self):
        with tempfile.TemporaryDirectory() as temporary:
            repository_root = Path(temporary)
            tool_root = self.populate(repository_root, version="0.1.0", draft=True)
            (repository_root / "LICENSE").unlink()
            (tool_root / "THIRD-PARTY-NOTICES.md").unlink()
            (tool_root / "CHANGELOG.en.md").write_text("# Changelog\n", encoding="utf-8")
            failed = {
                item["id"]
                for item in prerequisites.check(tool_root, repository_root)
                if not item["passed"]
            }
            self.assertIn("RC-VERSION", failed)
            self.assertIn("RC-LICENSE", failed)
            self.assertIn("RC-THIRD-PARTY", failed)
            self.assertIn("RC-CHANGELOG-EN", failed)
            self.assertTrue(any(item.startswith("RC-SPEC-") for item in failed))


if __name__ == "__main__":
    unittest.main()
