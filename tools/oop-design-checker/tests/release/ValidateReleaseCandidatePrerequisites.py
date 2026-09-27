#!/usr/bin/env python3
import argparse
import json
import re
import xml.etree.ElementTree as ET
from pathlib import Path

TARGET_VERSION = "1.0.0"
NOTICE_CANDIDATES = (
    "THIRD-PARTY-NOTICES.md",
    "THIRD-PARTY-NOTICES.txt",
    "THIRD-PARTY-NOTICES",
)
SPEC_FILES = (
    ("specification/object-oriented-design.md", "ドラフト"),
    ("specification/check-rules.md", "ドラフト"),
    ("specification/object-oriented-design.en.md", "draft"),
    ("specification/check-rules.en.md", "draft"),
)


def parse_args():
    parser = argparse.ArgumentParser()
    parser.add_argument("--root", default=".")
    parser.add_argument("--output")
    return parser.parse_args()


def non_empty_file(path):
    return path.is_file() and path.stat().st_size > 0


def first_heading(path):
    for line in path.read_text(encoding="utf-8-sig").splitlines():
        if line.startswith("# "):
            return line[2:].strip()
    return ""


def declared_version(root):
    props = root / "Directory.Build.props"
    if not props.exists():
        return None
    tree = ET.parse(props)
    version = tree.getroot().find(".//VersionPrefix")
    return version.text.strip() if version is not None and version.text else None


def changelog_has_version(path):
    if not path.exists():
        return False
    pattern = re.compile(r"^##\s+\[?1\.0\.0(?:\]|\s|$)", re.MULTILINE)
    return pattern.search(path.read_text(encoding="utf-8-sig")) is not None


def check(root):
    checks = []

    version = declared_version(root)
    checks.append({
        "id": "RC-VERSION",
        "passed": version == TARGET_VERSION,
        "detail": f"VersionPrefix={version!r}; expected {TARGET_VERSION!r}.",
    })

    license_path = root / "LICENSE"
    checks.append({
        "id": "RC-LICENSE",
        "passed": non_empty_file(license_path),
        "detail": "Root LICENSE must exist and be non-empty.",
    })

    notice = next(
        (root / name for name in NOTICE_CANDIDATES if non_empty_file(root / name)),
        None,
    )
    checks.append({
        "id": "RC-THIRD-PARTY",
        "passed": notice is not None,
        "detail": (
            f"Third-party notice: {notice.name}."
            if notice is not None
            else "A non-empty third-party notice file is required."
        ),
    })

    for relative, draft_marker in SPEC_FILES:
        path = root / relative
        heading = first_heading(path) if path.exists() else ""
        passed = bool(heading) and draft_marker.casefold() not in heading.casefold()
        checks.append({
            "id": "RC-SPEC-" + Path(relative).stem.upper().replace(".", "-"),
            "passed": passed,
            "detail": f"{relative} heading: {heading!r}; draft marker must be absent.",
        })

    for relative in ("CHANGELOG.md", "CHANGELOG.en.md"):
        checks.append({
            "id": "RC-CHANGELOG-" + ("JA" if relative == "CHANGELOG.md" else "EN"),
            "passed": changelog_has_version(root / relative),
            "detail": f"{relative} must contain a 1.0.0 release entry.",
        })

    return checks


def main():
    args = parse_args()
    root = Path(args.root).resolve()
    checks = check(root)
    result = {
        "version": 1,
        "targetVersion": TARGET_VERSION,
        "passed": all(item["passed"] for item in checks),
        "checks": checks,
    }
    text = json.dumps(result, indent=2, ensure_ascii=False) + "\n"
    if args.output:
        output = Path(args.output)
        output.parent.mkdir(parents=True, exist_ok=True)
        output.write_text(text, encoding="utf-8")
    print(text, end="")
    return 0 if result["passed"] else 1


if __name__ == "__main__":
    raise SystemExit(main())
