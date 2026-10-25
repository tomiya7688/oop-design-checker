"""{"責務": "Python版・CA・上書き・CI適用範囲の境界を検証します。"}"""

import contextlib
import hashlib
import importlib.metadata
import io
import json
import os
from pathlib import Path
import re
import ssl
import sys
import tempfile
import types
import unittest
from unittest.mock import Mock, patch

import audit_python_trust as audit


class PythonTrustTests(unittest.TestCase):
    """{"責務": "古い版や検査不能を成功にしない契約を検証します。"}"""

    def test_version_boundaries(self):
        """{"責務": "最低版・古い版・未認識版を区別します。"}"""
        for actual, minimum, expected in [
            ("26.2.1", "26.2.2", False), ("26.2.2", "26.2.2", True),
            ("26.10.0", "26.2.2", True), ("26.2.2rc1", "26.2.2", False),
            ("unknown", "26.2.2", False), ("2026.10.18", "2026.10.19", False),
            ("2026.10.23", "2026.10.19", True), ("0.13.3", "0.13.4", False),
            ("0.13.4", "0.13.4", True), ("4.5.0", "4.5.1", False),
            ("4.5.1", "4.5.1", True), ("26.3", "26.2.2", True),
        ]:
            with self.subTest(actual=actual, minimum=minimum):
                self.assertEqual(expected, audit.version_meets_minimum(actual, minimum))

    def test_fingerprint_checks_der_not_labels(self):
        """{"責務": "表示名やPEMコメントに依存せず問題DERを検出します。"}"""
        context = Mock()
        context.get_ca_certs.return_value = [b"unrelated", b"compromised"]
        fingerprint = hashlib.sha256(b"compromised").hexdigest()
        with patch.object(audit, "COMPROMISED_ROOT_SHA256", fingerprint):
            result = audit.inspect_ca_context(context)
        self.assertTrue(result["compromised_root_present"])
        self.assertEqual(2, result["ca_count"])
        context.get_ca_certs.assert_called_once_with(binary_form=True)

    def test_empty_ca_set_is_failure(self):
        """{"責務": "CAを読み込めなかった状態を成功と扱わないことを確認します。"}"""
        context = Mock()
        context.get_ca_certs.return_value = []
        instance = audit.PythonTrustAudit()
        instance.check_context("empty", context)
        self.assertTrue(instance.report["errors"])

    def test_missing_and_invalid_ca_bundles_are_failures(self):
        """{"責務": "欠落・不正なCAファイルを失敗として記録します。"}"""
        with tempfile.TemporaryDirectory() as directory:
            path = Path(directory) / "invalid.pem"
            path.write_text("not a certificate", encoding="ascii")
            for candidate in [path, path.with_name("missing.pem")]:
                instance = audit.PythonTrustAudit()
                instance.check_bundle("test", candidate)
                self.assertTrue(instance.report["errors"])

    def test_required_and_optional_packages(self):
        """{"責務": "必須パッケージの欠落と任意パッケージの未導入を区別します。"}"""
        with patch.object(importlib.metadata, "version", side_effect=importlib.metadata.PackageNotFoundError):
            instance = audit.PythonTrustAudit()
            instance.check_package("uv", required=False)
            self.assertFalse(instance.report["errors"])
            instance.check_package("pip")
            self.assertTrue(instance.report["errors"])
            self.assertIsNone(instance.report["packages"]["uv"])

    def test_old_package_is_failure(self):
        """{"責務": "実環境の古いパッケージを拒否します。"}"""
        with patch.object(importlib.metadata, "version", return_value="26.2.1"):
            instance = audit.PythonTrustAudit()
            instance.check_package("pip")
            self.assertTrue(instance.report["errors"])

    def test_overrides_and_configuration_do_not_expose_authentication(self):
        """{"責務": "明示CAを検査し、認証URLを結果へ出さないことを確認します。"}"""
        configuration = Mock()
        configuration.items.return_value = [
            ("global.index-url", "https://user:private-password@example.invalid"),
            ("global.cert", "configured-ca.pem"), ("global.trusted-host", "example.invalid")]
        module = types.SimpleNamespace(Configuration=Mock(return_value=configuration))
        with patch.dict(sys.modules, {"pip._internal.configuration": module}), patch.dict(
            os.environ, {"PIP_CERT": "env-ca.pem", "SSL_CERT_DIR": "deferred"}, clear=True
        ):
            instance = audit.PythonTrustAudit()
            with patch.object(instance, "check_bundle") as check:
                instance.check_overrides()
                check.assert_any_call("PIP_CERT", "env-ca.pem")
                check.assert_any_call("pip-config:global.cert", "configured-ca.pem")
        self.assertEqual(2, len(instance.report["errors"]))
        self.assertNotIn("private-password", json.dumps(instance.report))

    def test_configuration_error_is_failure(self):
        """{"責務": "pip設定を読めなかった場合に成功と扱わないことを確認します。"}"""
        module = types.SimpleNamespace(Configuration=Mock(side_effect=RuntimeError("private detail")))
        with patch.dict(sys.modules, {"pip._internal.configuration": module}), patch.dict(os.environ, {}, clear=True):
            instance = audit.PythonTrustAudit()
            instance.check_overrides()
        self.assertEqual(["pip configuration could not be inspected"], instance.report["errors"])

    def test_cli_exit_code_matches_report(self):
        """{"責務": "検査失敗をJSONと終了コードの両方に反映します。"}"""
        for passed in [False, True]:
            with self.subTest(passed=passed), patch.object(audit.PythonTrustAudit, "run", return_value={"passed": passed}):
                output = io.StringIO()
                with contextlib.redirect_stdout(output):
                    self.assertEqual(0 if passed else 1, audit.main())
                self.assertEqual({"passed": passed}, json.loads(output.getvalue()))

    def test_all_explicit_python_jobs_use_shared_setup(self):
        """{"責務": "Python実行がある既存ジョブへ共通更新経路が適用されることを確認します。"}"""
        root = Path(__file__).resolve().parents[4]
        for workflow in (root / ".github/workflows").glob("*.yml"):
            content = workflow.read_text(encoding="utf-8")
            if "\njobs:\n" not in content:
                continue
            pieces = re.split(r"\n  ([\w-]+):\n", "\n" + content.split("\njobs:\n", 1)[1])
            for name, body in zip(pieces[1::2], pieces[2::2]):
                if re.search(r"(?m)^\s+(?:run:.*\bpython\b|python\s|shell: python)", body):
                    with self.subTest(workflow=workflow.name, job=name):
                        self.assertIn("uses: ./.github/actions/setup-python-trust", body)

    def test_windows_cleanup_is_scoped_and_uses_the_audited_fingerprint(self):
        """{"責務": "Windows runnerの除去対象が監査と同じDER指紋で限定されることを確認します。"}"""
        root = Path(__file__).resolve().parents[4]
        content = (root / ".github/actions/setup-python-trust/action.yml").read_text(encoding="utf-8")
        self.assertIn("if: runner.os == 'Windows'", content)
        self.assertIn("$env:GITHUB_ACTIONS -ne 'true'", content)
        self.assertIn(audit.COMPROMISED_ROOT_SHA256, content)
        self.assertIn("if ($actualSha256 -eq $blockedSha256)", content)
        self.assertIn("$store.Remove($certificate)", content)

    def test_shared_policy_changes_trigger_consumers(self):
        """{"責務": "共通actionだけの変更でも該当PRワークフローが実行されることを確認します。"}"""
        root = Path(__file__).resolve().parents[4]
        for name in ["release.yml", "ui-appium.yml", "ui-ai-contract.yml", "ui-ai-panel.yml"]:
            content = (root / ".github/workflows" / name).read_text(encoding="utf-8")
            with self.subTest(workflow=name):
                self.assertIn("'.github/actions/setup-python-trust/**'", content)
                self.assertIn("'tools/oop-design-checker/tests/python-security/**'", content)


if __name__ == "__main__":
    unittest.main()
