"""{
  "責務": "ネットワークを使わずGitの差分集合を確定します。"
}"""

from pathlib import Path
import os
import subprocess


GIT_TIMEOUT_SECONDS = 30


class RepositoryChanges:
    """{
      "責務": "作業ツリーと分岐後の変更を所有します。",
      "フィールド": {"root": "実際のリポジトリルートです。"}
    }"""

    def __init__(self, root):
        """{
          "責務": "基準ディレクトリを保持します。",
          "処理": ["絶対パスに変換します。"],
          "引数": {"root": "Gitルートです。"}, "戻り値": null
        }"""
        self.root = Path(root).resolve()

    def git(self, *arguments):
        """{
          "責務": "読取専用Gitコマンドの失敗を伝えます。",
          "処理": ["Gitを実行します。", "UTF-8の結果を返します。"],
          "引数": {"arguments": "個別の引数です。"}, "戻り値": "標準出力です。"
        }"""
        return subprocess.run(
            ["git", *arguments],
            cwd=self.root,
            check=True,
            capture_output=True,
            timeout=GIT_TIMEOUT_SECONDS,
        ).stdout.decode("utf-8")

    def collect(self):
        """{
          "責務": "削除を含む変更パスと比較元を返します。",
          "処理": ["ルートとmain参照を確認します。", "差分と未追跡を統合します。", "安全なパスを確認します。"],
          "引数": {}, "戻り値": "比較元commitと変更パスです。"
        }"""
        actual_root = Path(self.git("rev-parse", "--show-toplevel").strip()).resolve()
        if actual_root != self.root:
            raise ValueError("Run from the repository containing check_local.py")
        base = self.git("merge-base", "HEAD", "refs/remotes/origin/main").strip()
        names = self.git("diff", "--name-only", "--no-renames", "-z", base).split("\0")
        names += self.git("ls-files", "--others", "--exclude-standard", "-z").split("\0")
        paths = sorted(set(filter(None, names)))
        if not paths:
            raise ValueError("No changed files; zero targets is not PASS")
        for name in paths:
            path = self.root / name
            if "\n" in name or "\r" in name or path.is_symlink():
                raise ValueError(f"Unsupported path: {name!r}")
            if not path.resolve().is_relative_to(self.root):
                raise ValueError(f"Path outside repository: {name!r}")
        return base, paths

    def check_untracked_whitespace(self):
        """{
          "責務": "Gitの通常diffに含まれない新規ファイルの空白も確認します。",
          "処理": ["未追跡ファイルを列挙します。", "空ファイルとの差分をGitで検査します。"],
          "引数": {}, "戻り値": null
        }"""
        names = self.git("ls-files", "--others", "--exclude-standard", "-z").split("\0")
        for name in filter(None, names):
            result = subprocess.run(
                ["git", "diff", "--no-index", "--check", "--", os.devnull, str(self.root / name)],
                cwd=self.root,
                capture_output=True,
                timeout=GIT_TIMEOUT_SECONDS,
            )
            # no-indexは正常な内容差でも1を返します。--checkの診断出力は失敗です。
            if result.returncode not in (0, 1) or result.stdout:
                raise ValueError(
                    f"Untracked whitespace check failed: {name!r}; "
                    + (result.stdout + result.stderr).decode("utf-8", errors="replace")
                )
