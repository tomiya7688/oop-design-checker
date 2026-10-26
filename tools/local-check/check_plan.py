"""{
  "責務": "変更された言語と既存の検査手段を対応付けます。"
}"""

from pathlib import Path
import json
import sys


TOOL = Path("tools/oop-design-checker")
FORMATTING = Path("tools/formatting")
CSHARP_SUFFIXES = {".cs", ".csproj", ".sln", ".slnx", ".props", ".targets"}
PRETTIER_SUFFIXES = {".md", ".json", ".yaml", ".yml"}
TEXT_NAMES = {".gitignore", ".editorconfig", ".prettierignore", "requirements.txt", "LICENSE"}


class CheckPlan:
    """{
      "責務": "今回の必須検査とCIへの委譲範囲を所有します。",
      "フィールド": {"root": "Gitルートです。", "paths": "削除も含む変更パスです。", "existing": "現存する変更ファイルです。", "runner": "結果の記録先です。"}
    }"""

    def __init__(self, root, paths, runner):
        """{
          "責務": "変更集合と検査実行先を保持します。",
          "処理": ["変更集合を保持します。", "削除済みファイルを整形対象から外します。"],
          "引数": {"root": "Gitルートです。", "paths": "変更名です。", "runner": "検査実行先です。"}, "戻り値": null
        }"""
        self.root, self.paths, self.runner = Path(root), list(map(Path, paths)), runner
        self.existing = [path for path in self.paths if (self.root / path).is_file()]

    def format_checks(self):
        """{
          "責務": "固定済みformatterを確認専用で呼び出します。",
          "処理": ["Pythonと文書の対象を選びます。", "既存formatterへ渡します。"],
          "引数": {}, "戻り値": null
        }"""
        python = [str(self.root / p) for p in self.existing if p.suffix == ".py"]
        if python or any(p.name == "ruff.toml" for p in self.paths):
            self.runner.command(
                "Ruff format",
                [
                    "ruff",
                    "format",
                    "--check",
                    "--config",
                    str(FORMATTING / "ruff.toml"),
                    *(python or ["tools/local-check"]),
                ],
            )
        documents = [
            str(self.root / p)
            for p in self.existing
            if p.suffix in PRETTIER_SUFFIXES or p.name == ".prettierrc.json"
        ]
        if documents:
            try:
                expected = json.loads(
                    (self.root / FORMATTING / "package.json").read_text(encoding="utf-8")
                )["devDependencies"]["prettier"]
                actual = json.loads(
                    (self.root / FORMATTING / "node_modules/prettier/package.json").read_text(
                        encoding="utf-8"
                    )
                )["version"]
                if actual != expected:
                    raise ValueError(f"Prettier {expected} required; found {actual}")
            except (OSError, ValueError, KeyError) as error:
                self.runner.record("FAIL", "Prettier dependency", str(error))
                return
            # npmラッパーのshellを避け、lockで固定したローカル実体を使います。
            self.runner.command(
                "Prettier",
                [
                    "node",
                    str(FORMATTING / "node_modules/prettier/bin/prettier.cjs"),
                    "--config",
                    str(FORMATTING / ".prettierrc.json"),
                    "--ignore-path",
                    str(FORMATTING / ".prettierignore"),
                    "--check",
                    *documents,
                ],
            )

    def csharp_checks(self):
        """{
          "責務": "C#の影響範囲を保守的にsolution全体で検査します。",
          "処理": ["C#関連変更を判定します。", "整形とビルドを確認します。", "既存の短いテストを実行します。"],
          "引数": {}, "戻り値": null
        }"""
        if not any(
            p.suffix in CSHARP_SUFFIXES or p.name in {"global.json", "dotnet-tools.json"}
            for p in self.paths
        ):
            self.runner.record("SKIP", "C# build/tests", "No C# project or source changes")
            return
        sources = [str(self.root / p) for p in self.existing if p.suffix == ".cs"]
        if sources:
            self.runner.command(
                "CSharpier", ["dotnet", "csharpier", "check", *sources], self.root / TOOL
            )
        built = self.runner.command(
            ".NET build/analyzers",
            [
                "dotnet",
                "build",
                str(TOOL / "OopDesignChecker.sln"),
                "-c",
                "Release",
                "--no-restore",
                "-p:EnableNETAnalyzers=true",
                "-p:AnalysisLevel=latest-recommended",
                "-p:EnforceCodeStyleInBuild=true",
                "-p:TreatWarningsAsErrors=true",
            ],
        )
        for name in (
            "OopDesignChecker.Tests",
            "OopDesignChecker.ContractTests",
            "OopDesignChecker.Gui.HeadlessTests",
        ):
            if built:
                self.runner.command(
                    name,
                    [
                        "dotnet",
                        "run",
                        "--project",
                        str(TOOL / f"tests/{name}/{name}.csproj"),
                        "-c",
                        "Release",
                        "--no-build",
                        "--no-restore",
                    ],
                )
            else:
                self.runner.record("UNVERIFIED", name, "Build failed; tests did not run")

    def python_tests(self):
        """{
          "責務": "既存の短いPythonテストを関連範囲から選びます。",
          "処理": ["テスト候補を検出します。", "変更との関係を確認します。", "0件を拒否する既存テスト入口へ渡します。"],
          "引数": {}, "戻り値": null
        }"""
        directories = {Path("tools/local-check")}
        all_tests = any(
            p.parts[0] == ".github"
            or p.suffix in CSHARP_SUFFIXES
            or p.name == "requirements.txt"
            or FORMATTING in p.parents
            for p in self.paths
        )
        candidates = {
            p.parent.relative_to(self.root)
            for p in (self.root / TOOL / "tests").glob("*/test_*.py")
        }
        directories.update(
            candidates
            if all_tests
            else {d for d in candidates if any(d in p.parents for p in self.paths)}
        )
        for path in self.paths:
            if (
                path.suffix == ".py"
                and not any(d in path.parents for d in directories)
                and path.name != "check_local.py"
            ):
                self.runner.record(
                    "UNVERIFIED", str(path), "No related short test suite registered"
                )
        for directory in sorted(directories):
            self.runner.command(
                f"unittest {directory}",
                [sys.executable, "-B", "tools/local-check/run_tests.py", str(directory)],
            )
