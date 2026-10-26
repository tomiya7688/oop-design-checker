"""{
  "責務": "既存unittestを再利用し、検出0件を拒否します。"
}"""

import sys
import unittest


def run(directory):
    """{
      "責務": "指定された短いテスト群を実行します。",
      "処理": ["既存テストを検出します。", "0件を拒否します。", "結果を終了値にします。"],
      "引数": {"directory": "テストディレクトリです。"}, "戻り値": "成功は0、失敗は1です。"
    }"""
    suite = unittest.defaultTestLoader.discover(directory, pattern="test_*.py")
    if suite.countTestCases() == 0:
        print(f"FAIL no tests discovered: {directory}", file=sys.stderr)
        return 1
    result = unittest.TextTestRunner(verbosity=1).run(suite)
    if result.testsRun == len(result.skipped):
        print(f"FAIL all tests skipped: {directory}", file=sys.stderr)
        return 1
    for test, reason in result.skipped:
        print(f"SKIP {test}: {reason}")
    return int(not result.wasSuccessful())


if __name__ == "__main__":
    raise SystemExit(run(sys.argv[1]))
