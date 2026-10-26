"""{
  "責務": "必須検査の結果と失敗理由を一つの要約に集約します。"
}"""

import os
from pathlib import Path
import subprocess
import time


COMMAND_TIMEOUT_SECONDS = 180


class CheckRunner:
    """{
      "責務": "検査結果と実行環境を所有します。",
      "フィールド": {"root": "検査ルートです。", "results": "状態・名前・原因です。", "environment": "子プロセス専用の環境です。", "started": "開始時刻です。"}
    }"""

    def __init__(self, root):
        """{
          "責務": "検査専用の出力先と結果を準備します。",
          "処理": ["状態を初期化します。", "一時ファイルをリポジトリ内に限定します。"],
          "引数": {"root": "Gitルートです。"}, "戻り値": null
        }"""
        self.root = Path(root)
        self.results = []
        self.started = time.monotonic()
        self.environment = os.environ.copy()
        temporary = self.root / "tools/local-check/.tmp"
        temporary.mkdir(parents=True, exist_ok=True)
        self.environment.update(
            TEMP=str(temporary),
            TMP=str(temporary),
            TMPDIR=str(temporary),
            PYTHONDONTWRITEBYTECODE="1",
            PYTHONUTF8="1",
            DOTNET_CLI_WORKLOAD_UPDATE_NOTIFY_DISABLE="true",
            DOTNET_CLI_TELEMETRY_OPTOUT="1",
        )

    def record(self, status, name, reason):
        """{
          "責務": "検査状態を保存します。", "処理": ["結果一覧へ追加します。"],
          "引数": {"status": "PASS/FAIL/SKIP/UNVERIFIEDです。", "name": "検査名です。", "reason": "原因です。"}, "戻り値": null
        }"""
        self.results.append((status, name, reason))

    def command(self, name, arguments, cwd=None):
        """{
          "責務": "必須コマンドを時間制限付きで実行します。",
          "処理": ["引数を表示します。", "コマンドを実行します。", "失敗を記録します。"],
          "引数": {"name": "検査名です。", "arguments": "shellを介さない引数列です。", "cwd": "実行場所です。"}, "戻り値": "成功したかです。"
        }"""
        print(f"RUN {name}: {arguments!r}", flush=True)
        try:
            subprocess.run(
                arguments,
                cwd=cwd or self.root,
                env=self.environment,
                check=True,
                timeout=COMMAND_TIMEOUT_SECONDS,
            )
        except (OSError, subprocess.SubprocessError) as error:
            self.record("FAIL", name, str(error))
            return False
        self.record("PASS", name, "exit 0")
        return True

    def finish(self):
        """{
          "責務": "未検証を成功扱いせず結果を表示します。",
          "処理": ["全状態を表示します。", "必須検査の不成立を終了値へ反映します。"],
          "引数": {}, "戻り値": "成功は0、失敗・未検証は1です。"
        }"""
        print(f"\nLocal check summary ({time.monotonic() - self.started:.1f}s)")
        for status, name, reason in self.results:
            print(f"{status} {name}: {reason}")
        print("Re-run: python check_local.py")
        passed = any(status == "PASS" for status, _, _ in self.results)
        return int(not passed or any(s in {"FAIL", "UNVERIFIED"} for s, _, _ in self.results))
