"""{"責務": "公開停止ゲートと、署名経路をPRで検査する接続を維持します。"}"""

from pathlib import Path
import re
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

    def test_final_release_and_fixture_use_shared_action(self):
        """{"責務": "最終25件の公開集合と無害なfixtureが共通actionを通ります。"}"""
        release = (ROOT / ".github/workflows/release.yml").read_text(encoding="utf-8")
        self.assertEqual(2, release.count("uses: ./.github/actions/attest-release"))
        self.assertIn("release-version: ${{ needs.version.outputs.product-version }}", release)
        self.assertIn("provenance-smoke-only", release)
        self.assertIn("not a product build", release)

    def test_pull_request_builds_have_no_signing_write_permissions(self):
        """{"責務": "PRから実行するビルドへ署名用の書込権限を与えません。"}"""
        release = (ROOT / ".github/workflows/release.yml").read_text(encoding="utf-8")
        for job, next_job in (("package", "gui-package"), ("gui-package", "release")):
            body = release.split(f"\n  {job}:\n", 1)[1].split(f"\n  {next_job}:\n", 1)[0]
            self.assertNotIn("id-token: write", body)
            self.assertNotIn("attestations: write", body)

    def test_privileged_external_actions_are_pinned(self):
        """{"責務": "署名権限を持つジョブと共通Python準備の外部actionをcommitへ固定します。"}"""
        release = (ROOT / ".github/workflows/release.yml").read_text(encoding="utf-8")
        privileged = release.split("\n  release:\n", 1)[1]
        setup = (ROOT / ".github/actions/setup-python-trust/action.yml").read_text(encoding="utf-8")
        references = re.findall(r"uses: (actions/[^\s]+)", privileged + setup)
        self.assertEqual(5, len(references))
        for reference in references:
            self.assertRegex(reference, r"^actions/[\w-]+@[0-9a-f]{40}$")
        self.assertEqual(2, privileged.count("persist-credentials: false"))

    def test_package_checksums_are_written_with_lf(self):
        """{"責務": "Windowsを含む両frontendのchecksum生成をLFに固定します。"}"""
        release = (ROOT / ".github/workflows/release.yml").read_text(encoding="utf-8")
        self.assertEqual(
            2,
            release.count("f'{digest}  {archive.name}\\n', encoding='utf-8', newline='\\n'"),
        )

    def test_action_is_pinned_and_never_ignores_verification_failure(self):
        """{"責務": "署名actionを固定し、gh検証の失敗を無視しない構成を確認します。"}"""
        action = (ROOT / ".github/actions/attest-release/action.yml").read_text(encoding="utf-8")
        self.assertIn("actions/attest@1e69f48acb82d1966a394da916b4c1698aa569d6", action)
        self.assertIn("verify_release_provenance.py verify", action)
        self.assertNotIn("continue-on-error", action)
        self.assertNotIn("|| true", action)


if __name__ == "__main__":
    unittest.main()
