#!/usr/bin/env python3
"""Wrap the unmodified GUI publish output for LaunchServices/XCTest (Mac2)."""

import argparse
import os
import plistlib
import re
import shutil
from pathlib import Path

BUNDLE_ID = "io.github.tomiya7688.oop-design-checker"
EXECUTABLE = "oop-design-checker-gui"
REQUIRED_FILES = (
    EXECUTABLE,
    f"{EXECUTABLE}.dll",
    f"{EXECUTABLE}.deps.json",
    f"{EXECUTABLE}.runtimeconfig.json",
    "BuildHost-netcore/Microsoft.CodeAnalysis.Workspaces.MSBuild.BuildHost.dll",
    "ReferenceAssemblies/System.Runtime.dll",
)


def require_payload(root: Path) -> None:
    missing = [name for name in REQUIRED_FILES if not (root / name).is_file()]
    if missing:
        raise ValueError(f"Incomplete GUI publish output under {root}: {', '.join(missing)}")
    if os.name != "nt" and not os.access(root / EXECUTABLE, os.X_OK):
        raise ValueError(f"GUI apphost is not executable: {root / EXECUTABLE}")


def validate_bundle(bundle: Path) -> str:
    """Reject a bare apphost before creating an Appium session."""
    bundle = bundle.resolve()
    if bundle.suffix != ".app" or not bundle.is_dir():
        raise ValueError(f"Mac2 requires the GUI .app bundle, not a bare executable: {bundle}")
    with (bundle / "Contents" / "Info.plist").open("rb") as stream:
        metadata = plistlib.load(stream)
    if (
        metadata.get("CFBundlePackageType") != "APPL"
        or metadata.get("CFBundleExecutable") != EXECUTABLE
        or metadata.get("CFBundleIdentifier") != BUNDLE_ID
    ):
        raise ValueError(f"Invalid OOP Design Checker application metadata: {bundle}")
    require_payload(bundle / "Contents" / "MacOS")
    return BUNDLE_ID


def create_bundle(publish: Path, bundle: Path, version: str) -> Path:
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
    require_payload(publish)

    contents = bundle / "Contents"
    # Keep the full dependency tree, file bytes, and executable permissions intact.
    shutil.copytree(publish, contents / "MacOS", symlinks=True)
    (contents / "Resources").mkdir()
    with (contents / "Info.plist").open("wb") as stream:
        plistlib.dump(
            {
                "CFBundleInfoDictionaryVersion": "6.0",
                "CFBundleIdentifier": BUNDLE_ID,
                "CFBundleExecutable": EXECUTABLE,
                "CFBundleName": "OOP Design Checker",
                "CFBundleDisplayName": "OOP Design Checker",
                "CFBundlePackageType": "APPL",
                "CFBundleShortVersionString": version,
                "CFBundleVersion": version,
                "NSHighResolutionCapable": True,
            },
            stream,
        )
    validate_bundle(bundle)
    return bundle


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--publish", type=Path, required=True)
    parser.add_argument("--bundle", type=Path, required=True)
    parser.add_argument("--version", required=True)
    args = parser.parse_args()
    try:
        print(create_bundle(args.publish, args.bundle, args.version))
    except (OSError, ValueError) as exc:
        parser.exit(1, f"macOS application bundle creation failed: {exc}\n")


if __name__ == "__main__":
    main()
