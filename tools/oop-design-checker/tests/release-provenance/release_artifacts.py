"""{"責務": "最終アーカイブとチェックサムの集合を所有し、欠落・混入・改変を拒否します。"}"""

import hashlib
from pathlib import Path
import re


RUNTIME_ARCHIVES = {
    "win-x64": "zip",
    "win-arm64": "zip",
    "linux-x64": "tar.gz",
    "linux-arm64": "tar.gz",
    "osx-x64": "tar.gz",
    "osx-arm64": "tar.gz",
}
MANIFEST_NAME = "SHA256SUMS.txt"


class ReleaseArtifacts:
    """{"責務": "期待する配布物の名前と実ファイルの整合性を保ちます。", "フィールド": {"directory": "成果物ディレクトリです。", "archive_names": "期待するアーカイブ名です。", "combined": "統合manifestを必要とするかです。"}}"""

    def __init__(self, directory, archive=None, release_version=None):
        """{"責務": "単一packageまたは全12配布物の期待集合を作ります。", "処理": ["引数を検証します。", "期待ファイル名を固定します。"], "引数": {"directory": "成果物の場所です。", "archive": "単一package名です。", "release_version": "全配布物の製品版です。"}, "戻り値": null}"""
        if bool(archive) == bool(release_version):
            raise ValueError("Specify exactly one archive or release version")
        self.directory = Path(directory)
        self.combined = bool(release_version)
        if release_version:
            if not re.fullmatch(r"\d+\.\d+\.\d+(?:-[A-Za-z0-9.-]+)?", release_version):
                raise ValueError("Invalid release version")
            self.archive_names = sorted(
                f"oop-design-checker{frontend}-{release_version}-{rid}.{extension}"
                for frontend in ("", "-gui")
                for rid, extension in RUNTIME_ARCHIVES.items()
            )
        else:
            if not re.fullmatch(r"[A-Za-z0-9][A-Za-z0-9._-]*\.(?:zip|tar\.gz)", archive):
                raise ValueError("Expected a plain archive basename")
            self.archive_names = [archive]

    def inspect(self, create_manifest=False):
        """{"責務": "実成果物を点検し、署名・検証の対象を返します。", "処理": ["ファイル集合を検証します。", "実SHA-256とsidecarを照合します。", "統合manifestを照合または初回作成します。"], "引数": {"create_manifest": "欠落した統合manifestの作成を許可するかです。"}, "戻り値": "対象ファイルの絶対パス一覧です。"}"""
        if self.directory.is_symlink() or not self.directory.is_dir():
            raise ValueError("Expected a real artifact directory")
        expected = set(self.archive_names) | {f"{name}.sha256" for name in self.archive_names}
        if self.combined:
            expected.add(MANIFEST_NAME)
        actual = {path.name for path in self.directory.iterdir()}
        permitted_missing = {MANIFEST_NAME} if self.combined and create_manifest else set()
        if actual - expected or expected - actual - permitted_missing:
            raise ValueError("Artifact set has missing or unexpected files")
        for name in actual:
            path = self.directory / name
            if path.is_symlink() or not path.is_file():
                raise ValueError("Artifact entries must be regular files")

        # checksumは実ファイルをストリームで読み、署名対象そのものに対応させます。
        lines = []
        for name in self.archive_names:
            with (self.directory / name).open("rb") as archive:
                digest = hashlib.file_digest(archive, "sha256").hexdigest()
            line = f"{digest}  {name}\n"
            if (self.directory / f"{name}.sha256").read_text(encoding="utf-8") != line:
                raise ValueError(f"Checksum mismatch: {name}")
            lines.append(line)

        if self.combined:
            manifest = self.directory / MANIFEST_NAME
            content = "".join(lines)
            if not manifest.exists() and create_manifest:
                manifest.write_text(content, encoding="utf-8", newline="\n")
            elif manifest.read_text(encoding="utf-8") != content:
                raise ValueError("Combined checksum manifest mismatch")
        return [(self.directory / name).resolve() for name in sorted(expected)]
