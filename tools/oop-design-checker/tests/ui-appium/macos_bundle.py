#!/usr/bin/env python3
"""GUI公開出力を変更せずLaunchServices/XCTest (Mac2)用に包む。"""

import argparse
import os
import plistlib
import re
import shutil
from pathlib import Path

アプリ識別子 = "io.github.tomiya7688.oop-design-checker"
実行ファイル名 = "oop-design-checker-gui"
必須ファイル一覧 = (
    実行ファイル名,
    f"{実行ファイル名}.dll",
    f"{実行ファイル名}.deps.json",
    f"{実行ファイル名}.runtimeconfig.json",
    "BuildHost-netcore/Microsoft.CodeAnalysis.Workspaces.MSBuild.BuildHost.dll",
    "ReferenceAssemblies/System.Runtime.dll",
)


def 配布物を検証する(root: Path) -> None:
    missing = [name for name in 必須ファイル一覧 if not (root / name).is_file()]
    if missing:
        raise ValueError(f"Incomplete GUI publish output under {root}: {', '.join(missing)}")
    if os.name != "nt" and not os.access(root / 実行ファイル名, os.X_OK):
        raise ValueError(f"GUI apphost is not executable: {root / 実行ファイル名}")


def アプリバンドルを検証する(bundle: Path) -> str:
    """Reject a bare apphost before creating an Appium session."""
    bundle = bundle.resolve()
    if bundle.suffix != ".app" or not bundle.is_dir():
        raise ValueError(f"Mac2 requires the GUI .app bundle, not a bare executable: {bundle}")
    with (bundle / "Contents" / "Info.plist").open("rb") as stream:
        metadata = plistlib.load(stream)
    if (
        metadata.get("CFBundlePackageType") != "APPL"
        or metadata.get("CFBundleExecutable") != 実行ファイル名
        or metadata.get("CFBundleIdentifier") != アプリ識別子
    ):
        raise ValueError(f"Invalid OOP Design Checker application metadata: {bundle}")
    配布物を検証する(bundle / "Contents" / "MacOS")
    return アプリ識別子


def アプリバンドルを作る(publish: Path, bundle: Path, version: str) -> Path:
    publish = publish.resolve(strict=True)
    bundle = bundle.resolve()
    if not publish.is_dir():
        raise ValueError(f"Publish path must be a directory: {publish}")
    if bundle.suffix != ".app":
        raise ValueError("Bundle destination must end in .app")
    if bundle == publish or bundle.is_relative_to(publish) or publish.is_relative_to(bundle):
        raise ValueError("Bundle destination must be outside the publish tree")
    if bundle.exists():
        raise FileExistsError(f"Refusing to overwrite an existing bundle: {bundle}")
    if not re.fullmatch(r"[0-9]+\.[0-9]+\.[0-9]+", version):
        raise ValueError(f"Expected a three-part product version, found {version!r}")
    配布物を検証する(publish)

    contents = bundle / "Contents"
    # 依存関係ツリーとファイル内容と実行権限をすべて維持する。
    shutil.copytree(publish, contents / "MacOS", symlinks=True)
    (contents / "Resources").mkdir()
    with (contents / "Info.plist").open("wb") as stream:
        plistlib.dump(
            {
                "CFBundleInfoDictionaryVersion": "6.0",
                "CFBundleIdentifier": アプリ識別子,
                "CFBundleExecutable": 実行ファイル名,
                "CFBundleName": "OOP Design Checker",
                "CFBundleDisplayName": "OOP Design Checker",
                "CFBundlePackageType": "APPL",
                "CFBundleShortVersionString": version,
                "CFBundleVersion": version,
                "NSHighResolutionCapable": True,
            },
            stream,
        )
    アプリバンドルを検証する(bundle)
    return bundle


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--publish", type=Path, required=True)
    parser.add_argument("--bundle", type=Path, required=True)
    parser.add_argument("--version", required=True)
    args = parser.parse_args()
    try:
        print(アプリバンドルを作る(args.publish, args.bundle, args.version))
    except (OSError, ValueError) as exc:
        parser.exit(1, f"macOS application bundle creation failed: {exc}\n")


if __name__ == "__main__":
    main()
