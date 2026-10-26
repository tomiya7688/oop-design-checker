"""{
  "責務": "差分の取りこぼしと失敗の成功扱いを再現して防ぎます。"
}"""

import contextlib
import io
from pathlib import Path
import subprocess
import sys
import tempfile
import unittest
from unittest.mock import patch

from changed_files import RepositoryChanges
from check_plan import CheckPlan
from check_runner import CheckRunner
from local_check import check_syntax, main
from run_tests import run


class LocalCheckTests(unittest.TestCase):
    """{
      "責務": "公開入口の選択と合否を確認します。",
      "フィールド": {"root": "テスト専用のGitリポジトリです。", "temporary": "終了時に破棄する領域です。"}
    }"""

    def setUp(self):
        """{
          "責務": "独立したGitと検査環境を準備します。",
          "処理": ["一時領域を用意します。"],
          "引数": {}, "戻り値": null
        }"""
        self.temporary = tempfile.TemporaryDirectory()
        self.addCleanup(self.temporary.cleanup)
        self.root = Path(self.temporary.name)

    def git(self, *arguments):
        """{
          "責務": "fixture内だけでGit操作を実行します。",
          "処理": ["テスト用Gitを実行します。"],
          "引数": {"arguments": "引数列です。"}, "戻り値": "標準出力です。"
        }"""
        return subprocess.run(
            ["git", *arguments], cwd=self.root, check=True, capture_output=True
        ).stdout

    def write(self, name, text):
        """{
          "責務": "fixtureのファイルを用意します。",
          "処理": ["親を作成します。", "UTF-8で保存します。"],
          "引数": {"name": "相対名です。", "text": "内容です。"}, "戻り値": null
        }"""
        path = self.root / name
        path.parent.mkdir(parents=True, exist_ok=True)
        path.write_text(text, encoding="utf-8", newline="\n")

    def test_clean_tree_is_not_pass(self):
        """{
          "責務": "対象0件を成功にしません。", "処理": ["cleanで実行します。"],
          "引数": {}, "戻り値": null
        }"""
        changes = RepositoryChanges(self.root)
        with patch.object(changes, "git", side_effect=[str(self.root), "a" * 40, "", ""]):
            with self.assertRaisesRegex(ValueError, "zero targets"):
                changes.collect()
        with patch("local_check.RepositoryChanges.collect", side_effect=ValueError("zero targets")):
            with contextlib.redirect_stdout(io.StringIO()) as output:
                self.assertEqual(1, main(self.root))
        self.assertIn("zero targets", output.getvalue())

    def test_discovers_committed_staged_unstaged_and_untracked(self):
        """{
          "責務": "すべての差分状態を統合します。", "処理": ["各状態を作ります。", "名前を比較します。"],
          "引数": {}, "戻り値": null
        }"""
        self.git("init", "-q")
        self.git("config", "user.email", "local-check@example.invalid")
        self.git("config", "user.name", "Local check fixture")
        self.git("config", "core.autocrlf", "false")
        self.write("initial.md", "initial\n")
        self.git("add", ".")
        self.git("-c", "commit.gpgsign=false", "commit", "-qm", "fixture")
        self.git("update-ref", "refs/remotes/origin/main", "HEAD")
        self.write("committed.py", "value = 1\n")
        self.git("add", "committed.py")
        self.git("-c", "commit.gpgsign=false", "commit", "-qm", "change")
        self.write("staged.py", "value = 2\n")
        self.git("add", "staged.py")
        self.write("initial.md", "modified\n")
        self.write("日本語 space.py", "value = 3\n")
        _, paths = RepositoryChanges(self.root).collect()
        self.assertEqual(["committed.py", "initial.md", "staged.py", "日本語 space.py"], paths)
        changes = RepositoryChanges(self.root)
        changes.check_untracked_whitespace()
        self.write("bad-whitespace.py", "value = 1  \n")
        with self.assertRaisesRegex(ValueError, "Untracked whitespace"):
            changes.check_untracked_whitespace()

    def test_missing_main_reference_fails(self):
        """{
          "責務": "基準参照不足を成功にしません。", "処理": ["参照を除去して検出します。"],
          "引数": {}, "戻り値": null
        }"""
        changes = RepositoryChanges(self.root)
        missing = subprocess.CalledProcessError(128, ["git", "merge-base"])
        with patch.object(changes, "git", side_effect=[str(self.root), missing]):
            with self.assertRaises(subprocess.CalledProcessError):
                changes.collect()

    def test_deleted_csharp_source_still_selects_build(self):
        """{
          "責務": "削除されたC#もビルド対象にします。", "処理": ["削除パスだけで計画します。"],
          "引数": {}, "戻り値": null
        }"""
        runner = CheckRunner(self.root)
        with patch.object(runner, "command", return_value=True) as command:
            CheckPlan(self.root, ["Removed.cs"], runner).csharp_checks()
        self.assertEqual(4, command.call_count)
        self.assertIn("--no-restore", command.call_args_list[0].args[1])

    def test_failed_build_does_not_run_stale_tests(self):
        """{
          "責務": "失敗後に古いバイナリのテストを成功扱いしません。", "処理": ["ビルド失敗を注入します。"],
          "引数": {}, "戻り値": null
        }"""
        runner = CheckRunner(self.root)
        with patch.object(runner, "command", return_value=False) as command:
            CheckPlan(self.root, ["Removed.cs"], runner).csharp_checks()
        self.assertEqual(1, command.call_count)
        self.assertEqual(3, sum(status == "UNVERIFIED" for status, _, _ in runner.results))

    def test_syntax_error_and_unknown_input_fail(self):
        """{
          "責務": "構文エラーと未対応形式を検出します。", "処理": ["不正コードと未知形式を検査します。"],
          "引数": {}, "戻り値": null
        }"""
        self.write("broken.py", "def broken(\n")
        self.write("unknown.xyz", "unknown")
        runner = CheckRunner(self.root)
        check_syntax(self.root, ["broken.py", "unknown.xyz"], runner)
        with contextlib.redirect_stdout(io.StringIO()):
            self.assertEqual(1, runner.finish())
        self.assertEqual(["FAIL", "UNVERIFIED"], [result[0] for result in runner.results])

    def test_short_test_failure_is_nonzero(self):
        """{
          "責務": "実際に失敗する短いテストを拒否します。", "処理": ["失敗テストを作って実行します。"],
          "引数": {}, "戻り値": null
        }"""
        directory = self.root / "suite"
        self.write(
            "suite/test_failure.py",
            "import unittest\nclass Failure(unittest.TestCase):\n def test_fail(self): self.fail('intentional')\n",
        )
        with contextlib.redirect_stderr(io.StringIO()):
            self.assertEqual(1, run(str(directory)))

    def test_no_discovered_tests_is_nonzero(self):
        """{
          "責務": "テスト0件を拒否します。", "処理": ["空ディレクトリを検査します。"],
          "引数": {}, "戻り値": null
        }"""
        with contextlib.redirect_stderr(io.StringIO()):
            self.assertEqual(1, run(str(self.root)))

    def test_all_skipped_tests_are_not_pass(self):
        """{
          "責務": "全テストskipを成功にしません。", "処理": ["全件skipのfixtureを実行します。"],
          "引数": {}, "戻り値": null
        }"""
        self.write(
            "skipped/test_skipped.py",
            "import unittest\n@unittest.skip('fixture')\nclass Skipped(unittest.TestCase):\n def test_skip(self): pass\n",
        )
        with contextlib.redirect_stderr(io.StringIO()):
            self.assertEqual(1, run(str(self.root / "skipped")))

    def test_missing_dependency_and_timeout_fail(self):
        """{
          "責務": "依存不足と時間超過を失敗にします。", "処理": ["各例外を注入して終了値を確認します。"],
          "引数": {}, "戻り値": null
        }"""
        for error in (FileNotFoundError("missing"), subprocess.TimeoutExpired("fixture", 180)):
            runner = CheckRunner(self.root)
            with patch("check_runner.subprocess.run", side_effect=error):
                self.assertFalse(runner.command("required", ["missing"]))
            with contextlib.redirect_stdout(io.StringIO()):
                self.assertEqual(1, runner.finish())

    def test_fixture_restore_has_no_remote_sources(self):
        """{
          "責務": "fixtureの復元をローカルだけに制限します。", "処理": ["一時領域のNuGet設定を確認します。"],
          "引数": {}, "戻り値": null
        }"""
        runner = CheckRunner(self.root)
        config = Path(runner.environment["TEMP"]) / "NuGet.Config"
        self.assertIn(
            "<packageSources><clear /></packageSources>", config.read_text(encoding="utf-8")
        )

    def test_concurrent_runs_use_separate_temporary_directories(self):
        """{
          "責務": "同じworktreeでの実行を一時領域で分離します。", "処理": ["二つの実行環境を比較します。"],
          "引数": {}, "戻り値": null
        }"""
        first, second = CheckRunner(self.root), CheckRunner(self.root)
        self.assertNotEqual(first.environment["TEMP"], second.environment["TEMP"])
        for runner in (first, second):
            self.assertTrue((Path(runner.environment["TEMP"]) / "NuGet.Config").is_file())

    def test_temporary_directory_error_has_a_failure_summary(self):
        """{
          "責務": "出力先を準備できない場合も失敗を集約します。", "処理": ["権限不足を注入します。"],
          "引数": {}, "戻り値": null
        }"""
        with patch("local_check.CheckRunner", side_effect=PermissionError("fixture")):
            with contextlib.redirect_stdout(io.StringIO()) as output:
                self.assertEqual(1, main(self.root))
        self.assertIn("FAIL temporary output", output.getvalue())

    def test_editorconfig_selects_build_and_existing_short_tests(self):
        """{
          "責務": "診断設定だけの変更でもC#検査を選びます。", "処理": ["各階層の設定名で計画します。"],
          "引数": {}, "戻り値": null
        }"""
        for name in (
            ".editorconfig",
            "tools/.editorconfig",
            "tools/oop-design-checker/.editorconfig",
        ):
            runner = CheckRunner(self.root)
            with patch.object(runner, "command", return_value=True) as command:
                CheckPlan(self.root, [name], runner).csharp_checks()
            self.assertEqual(4, command.call_count)
            self.assertEqual(".NET build/analyzers", command.call_args_list[0].args[0])

    def test_cleanup_removes_only_its_own_run(self):
        """{
          "責務": "他の実行や親領域を削除しません。", "処理": ["同時実行の片方だけを終了します。"],
          "引数": {}, "戻り値": null
        }"""
        first, second = CheckRunner(self.root), CheckRunner(self.root)
        first.record("PASS", "fixture", "completed")
        with contextlib.redirect_stdout(io.StringIO()):
            self.assertEqual(0, first.finish())
        self.assertFalse(Path(first.environment["TEMP"]).exists())
        self.assertTrue(Path(second.environment["TEMP"]).is_dir())

    def test_real_child_failure_reaches_summary(self):
        """{
          "責務": "実子プロセスの非0終了を集約します。", "処理": ["失敗コマンドを実行します。"],
          "引数": {}, "戻り値": null
        }"""
        runner = CheckRunner(self.root)
        self.assertFalse(
            runner.command("intentional failure", [sys.executable, "-c", "raise SystemExit(3)"])
        )
        self.assertEqual("FAIL", runner.results[0][0])


if __name__ == "__main__":
    unittest.main()
