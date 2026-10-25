"""{"責務": "実行中Pythonの版と実CA集合を検査します。失効・交差署名経路の検証は代替しません。"}"""

import hashlib
import importlib
import importlib.metadata
import json
import os
from pathlib import Path
import re
import ssl
import sys

# Python公式の2026-10-20告知に基づく最低版です。
MINIMUM_VERSIONS = {"pip": "26.2.2", "certifi": "2026.10.19", "uv": "0.13.4", "sigstore": "4.5.1"}
# emSign ECC Root CA - G3のDER SHA-256です。表示名には依存しません。
COMPROMISED_ROOT_SHA256 = "86a1ecba089c4a8d3bbe2734c612ba341d813e043cf9e8a862cd5c57a36bbe6b"


def version_meets_minimum(actual, minimum):
    """{"責務": "最低版未満と未認識の版を拒否します。", "処理": ["安定版の数値を比較します。"], "引数": {"actual": "実際の版です。", "minimum": "最低版です。"}, "戻り値": "最低版以上ならtrueです。"}"""
    def parts(value):
        """{"責務": "安定版を数値へ変換します。", "処理": ["二要素または三要素の版を解析します。"], "引数": {"value": "版の文字列です。"}, "戻り値": "数値の組、またはnullです。"}"""
        match = re.fullmatch(r"(\d+)\.(\d+)(?:\.(\d+))?", value)
        return tuple(int(part or 0) for part in match.groups()) if match else None

    actual_parts, minimum_parts = parts(actual), parts(minimum)
    return actual_parts is not None and minimum_parts is not None and actual_parts >= minimum_parts


def inspect_ca_context(context):
    """{"責務": "実CA集合の問題ルートを調べます。", "処理": ["DERのSHA-256を比較します。"], "引数": {"context": "検査するSSL contextです。"}, "戻り値": "CA数と問題ルートの有無です。"}"""
    certificates = context.get_ca_certs(binary_form=True)
    return {"ca_count": len(certificates), "compromised_root_present": any(
        hashlib.sha256(certificate).hexdigest() == COMPROMISED_ROOT_SHA256 for certificate in certificates)}


class PythonTrustAudit:
    """{"責務": "実行環境の検査結果を所有し、確認不能を成功扱いしません。", "フィールド": {"report": "Python、版、実CA、検査エラーです。"}}"""

    def __init__(self):
        """{"責務": "実行環境の識別情報を記録します。", "処理": ["実行中Pythonと空の結果を用意します。"], "引数": {}, "戻り値": null}"""
        self.report = {"python": sys.executable, "python_version": sys.version.split()[0],
                       "packages": {}, "trust_sources": [], "errors": [],
                       "default_verify_paths": ssl.get_default_verify_paths()._asdict(),
                       "boundary": "Static CA membership only; not a revocation or cross-signed-chain rejection test."}

    def check_package(self, name, required=True):
        """{"責務": "パッケージ版を最低版と比較します。", "処理": ["未導入と古い版を区別します。"], "引数": {"name": "パッケージ名です。", "required": "必須かを指定します。"}, "戻り値": null}"""
        try:
            actual = importlib.metadata.version(name)
        except importlib.metadata.PackageNotFoundError:
            self.report["packages"][name] = None
            if required:
                self.report["errors"].append(f"{name}: not installed")
            return
        self.report["packages"][name] = actual
        if not version_meets_minimum(actual, MINIMUM_VERSIONS[name]):
            self.report["errors"].append(f"{name}: minimum {MINIMUM_VERSIONS[name]} required")

    def check_context(self, name, context, path=None):
        """{"責務": "実CA集合を記録し、空や問題ルート入りなら失敗にします。", "処理": ["検査結果とエラーを記録します。"], "引数": {"name": "信頼元です。", "context": "実際のcontextです。", "path": "実CAファイルのパスです。"}, "戻り値": null}"""
        result = {"name": name, "path": path, **inspect_ca_context(context)}
        self.report["trust_sources"].append(result)
        if result["ca_count"] == 0 or result["compromised_root_present"]:
            self.report["errors"].append(f"{name}: empty or compromised CA set")

    def check_bundle(self, name, path):
        """{"責務": "実CAファイルだけを独立したcontextで調べます。", "処理": ["読取不能も失敗として記録します。"], "引数": {"name": "信頼元です。", "path": "実CAファイルです。"}, "戻り値": null}"""
        try:
            context = ssl.SSLContext(ssl.PROTOCOL_TLS_CLIENT)
            bundle = Path(path).read_bytes()
            context.load_verify_locations(cadata=bundle.decode("ascii"))
            self.check_context(name, context, str(Path(path).resolve()))
            self.report["trust_sources"][-1]["bundle_sha256"] = hashlib.sha256(bundle).hexdigest()
        except (OSError, ValueError, TypeError):
            self.report["errors"].append(f"{name}: CA bundle could not be loaded")

    def check_overrides(self):
        """{"責務": "CA上書きとpipのTLS検証回避を調べます。", "処理": ["CAファイルを検査し、遅延ディレクトリとtrusted-hostを拒否します。"], "引数": {}, "戻り値": null}"""
        if os.environ.get("SSL_CERT_DIR"):
            self.report["errors"].append("SSL_CERT_DIR: lazy CA directory requires separate verification")
        for name in ("SSL_CERT_FILE", "REQUESTS_CA_BUNDLE", "CURL_CA_BUNDLE", "PIP_CERT"):
            if os.environ.get(name):
                self.check_bundle(name, os.environ[name])
        try:
            from pip._internal.configuration import Configuration
            configuration = Configuration(isolated=False)
            configuration.load()
            for key, value in configuration.items():
                if key.endswith(".cert"):
                    self.check_bundle(f"pip-config:{key}", value)
                if key.endswith(".trusted-host") and value:
                    self.report["errors"].append("pip trusted-host disables normal TLS verification")
        except Exception:
            # 認証URLや設定値をログへ出さず、検査不能だけを記録します。
            self.report["errors"].append("pip configuration could not be inspected")

    def run(self):
        """{"責務": "パッケージと各実CAを読み取り専用で検査します。", "処理": ["版、CA、既定context、明示上書きを確認します。"], "引数": {}, "戻り値": "検査結果です。"}"""
        for name in MINIMUM_VERSIONS:
            self.check_package(name, required=name in ("pip", "certifi"))
        for name, module_name in (("certifi", "certifi"), ("pip-vendored-certifi", "pip._vendor.certifi")):
            try:
                self.check_bundle(name, importlib.import_module(module_name).where())
            except (ImportError, AttributeError):
                self.report["errors"].append(f"{name}: CA source unavailable")
        try:
            self.check_context("python-default-context", ssl.create_default_context())
        except (OSError, ValueError):
            self.report["errors"].append("python-default: CA source unavailable")
        self.check_overrides()
        self.report["passed"] = not self.report["errors"]
        return self.report


def main():
    """{"責務": "JSONを表示し検査失敗を終了コードへ反映します。", "処理": ["監査を実行します。"], "引数": {}, "戻り値": "成功は0、失敗は1です。"}"""
    report = PythonTrustAudit().run()
    print(json.dumps(report, ensure_ascii=False, indent=2))
    return 0 if report["passed"] else 1


if __name__ == "__main__":
    raise SystemExit(main())
