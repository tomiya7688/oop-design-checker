"""{"責務": "公開停止ゲートと、署名経路をPRで検査する接続を維持します。"}"""

from pathlib import Path
import unittest


ROOT = Path(__file__).resolve().parents[4]


class ReleaseWorkflowTests(unittest.TestCase):
    """{"責務": "現在のworkflow構造に対する回帰を検出します。Actions実行の代替にはしません。"}"""

    def test_native_signing_block_precedes_only_publication_command(self):
        """{"責務": "native署名未導入の間、公開コマンドへ到達できないことを検査します。"}"""
        release = (ROOT / ".github/workflows/release.yml").read_text(encoding="utf-8")
        block = release.split(
            "      - name: Block publication until native signing is implemented\n", 1
        )[1].split("      - name:", 1)[0]
        self.assertIn("          exit 1\n", block)
        self.assertNotIn("if:", block)
        self.assertNotIn("continue-on-error", release)
        self.assertLess(
            release.index("Block publication until native signing"),
            release.index("gh release create"),
        )
        self.assertEqual(1, release.count("gh release create"))

    def test_all_final_sets_use_shared_action(self):
        """{"責務": "CUI・GUI・統合manifestの全経路が共通actionを通ります。"}"""
        release = (ROOT / ".github/workflows/release.yml").read_text(encoding="utf-8")
        self.assertEqual(4, release.count("uses: ./.github/actions/attest-release"))
        self.assertIn("release-version: ${{ needs.version.outputs.product-version }}", release)
        self.assertIn("provenance-smoke-only", release)
        self.assertIn("not a product build", release)

    def test_action_is_pinned_and_never_ignores_verification_failure(self):
        """{"責務": "署名actionを固定し、gh検証の失敗を無視しない構成を確認します。"}"""
        action = (ROOT / ".github/actions/attest-release/action.yml").read_text(encoding="utf-8")
        self.assertIn("actions/attest@1e69f48acb82d1966a394da916b4c1698aa569d6", action)
        self.assertIn("verify_release_provenance.py verify", action)
        self.assertNotIn("continue-on-error", action)
        self.assertNotIn("|| true", action)


if __name__ == "__main__":
    unittest.main()
