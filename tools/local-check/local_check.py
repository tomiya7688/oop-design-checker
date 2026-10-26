"""{
  "責務": "差分・構文・既存検査の結果を単一入口でまとめます。"
}"""

import ast
import json
import sys
import subprocess
import tomllib
import xml.etree.ElementTree as ET

from changed_files import RepositoryChanges
from check_plan import CSHARP_SUFFIXES, PRETTIER_SUFFIXES, TEXT_NAMES, CheckPlan
from check_runner import CheckRunner


def check_syntax(root, paths, runner):
    """{
      "責務": "変更ファイルを実行せず構文を検査します。",
      "処理": ["対応言語を判定します。", "構文エラーと未対応形式を記録します。"],
      "引数": {"root": "Gitルートです。", "paths": "変更ファイルです。", "runner": "結果の記録先です。"}, "戻り値": null
    }"""
    for name in paths:
        path = root / name
        if not path.exists():
            if (
                path.suffix not in CSHARP_SUFFIXES | PRETTIER_SUFFIXES | {".py", ".toml"}
                and path.name not in TEXT_NAMES
            ):
                runner.record("UNVERIFIED", name, "Unsupported deleted input")
            continue  # 削除はGit整合性と関連テストで検証します。
        try:
            if path.suffix == ".py":
                ast.parse(path.read_bytes(), filename=name)
            elif path.suffix == ".json":
                json.loads(path.read_text(encoding="utf-8-sig"))
            elif path.suffix == ".toml":
                tomllib.loads(path.read_text(encoding="utf-8"))
            elif path.suffix in {".csproj", ".props", ".targets", ".slnx"}:
                ET.parse(path)
            elif (
                path.suffix not in CSHARP_SUFFIXES | PRETTIER_SUFFIXES
                and path.name not in TEXT_NAMES
            ):
                runner.record(
                    "UNVERIFIED", name, "Unsupported file type; add a check before claiming PASS"
                )
                continue
        except (OSError, ValueError, SyntaxError, ET.ParseError) as error:
            runner.record("FAIL", f"syntax {name}", str(error))
            continue
        runner.record(
            "PASS", f"scope/syntax {name}", "Supported input; formatter/build checks follow"
        )


def main(root):
    """{
      "責務": "必要なローカル検査を自動選択して実行します。",
      "処理": ["差分を確定します。", "整合性と構文を検査します。", "整形と短いテストを確認します。", "一つの要約を返します。"],
      "引数": {"root": "Gitルートです。"}, "戻り値": "必須検査の成功時のみ0です。"
    }"""
    try:
        runner = CheckRunner(root)
    except (OSError, ValueError) as error:
        print(f"Local check summary\nFAIL temporary output: {error}\nRe-run: python check_local.py")
        return 1
    try:
        changes = RepositoryChanges(root)
        base, paths = changes.collect()
    except (OSError, ValueError, UnicodeError, subprocess.SubprocessError) as error:
        runner.record("FAIL", "change discovery", str(error))
        return runner.finish()
    print(f"Base: {base}; changed files: {len(paths)}", flush=True)
    runner.command("git diff --check (base)", ["git", "diff", "--check", base])
    runner.command("git diff --check (index)", ["git", "diff", "--check", "--cached"])
    try:
        changes.check_untracked_whitespace()
        runner.record("PASS", "untracked diff --check", "Git checked all new files")
    except (OSError, ValueError, subprocess.SubprocessError) as error:
        runner.record("FAIL", "untracked diff --check", str(error))
    check_syntax(root, paths, runner)
    plan = CheckPlan(root, paths, runner)
    plan.format_checks()
    plan.csharp_checks()
    plan.python_tests()
    runner.record(
        "SKIP",
        "CI-only",
        "Appium, visual regression, multiple OS/RID, release and native signing remain CI/release gates",
    )
    return runner.finish()


if __name__ == "__main__":
    sys.exit("Use python check_local.py from the repository root")
