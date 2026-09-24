#!/usr/bin/env python3
import argparse
import json
import shutil
from pathlib import Path

APPIUM_SCENARIOS = (
    "01-cancel",
    "02-fixture-analysis",
    "03-filter-detail",
    "04-language-resize",
    "05-config-editor",
    "06-export",
    "07-source-open-fallback",
    "08-error-state",
)
HEADLESS_SCENARIOS = (
    "ready-ja-small",
    "ready-ja-default",
    "ready-ja-large",
    "ready-en-default",
    "fixture-ja-default",
    "filter-detail-oop106-ja",
    "fixture-en-default",
    "error-ja-default",
    "config-editor-ja-default",
)
VISUAL_SELECTION = (
    ("windows", "02-fixture-analysis", ("after.png",), "windows-fixture.png"),
    ("windows", "04-language-resize", ("resized.png", "after.png"), "windows-english-resized.png"),
    ("windows", "08-error-state", ("after.png",), "windows-error.png"),
    ("macos", "02-fixture-analysis", ("after.png",), "macos-fixture.png"),
    ("macos", "04-language-resize", ("resized.png", "after.png"), "macos-english-resized.png"),
    ("macos", "08-error-state", ("after.png",), "macos-error.png"),
    ("headless", "ready-ja-small", ("actual.png",), "headless-ja-small.png"),
    ("headless", "ready-en-default", ("actual.png",), "headless-en-default.png"),
    ("headless", "filter-detail-oop106-ja", ("actual.png",), "headless-filter-detail.png"),
    ("headless", "config-editor-ja-default", ("actual.png",), "headless-config-editor.png"),
)


def parse_args():
    parser = argparse.ArgumentParser()
    parser.add_argument("--windows", required=True)
    parser.add_argument("--macos", required=True)
    parser.add_argument("--headless", required=True)
    parser.add_argument("--output", required=True)
    parser.add_argument("--candidate-sha", required=True)
    return parser.parse_args()


def read_json(path):
    return json.loads(path.read_text(encoding="utf-8-sig"))


def find_scenario(root, scenario, required_files):
    candidates = []
    for directory in root.rglob(scenario):
        if not directory.is_dir():
            continue
        if all((directory / name).exists() for name in required_files):
            candidates.append(directory)
    if len(candidates) != 1:
        raise RuntimeError(
            f"Expected one {scenario!r} under {root}, found {len(candidates)}."
        )
    return candidates[0]


def find_file(root, relative_suffix):
    matches = [
        path
        for path in root.rglob(Path(relative_suffix).name)
        if path.is_file() and path.as_posix().endswith(relative_suffix)
    ]
    if len(matches) != 1:
        raise RuntimeError(
            f"Expected one file ending with {relative_suffix!r} under {root}, found {len(matches)}."
        )
    return matches[0]


def collect_appium(root):
    states = {}
    expected = {}
    actions = {}
    directories = {}
    for scenario in APPIUM_SCENARIOS:
        directory = find_scenario(
            root,
            scenario,
            ("expected.json", "ui-state.json", "action-log.json"),
        )
        directories[scenario] = directory
        states[scenario] = read_json(directory / "ui-state.json")
        expected[scenario] = read_json(directory / "expected.json")
        actions[scenario] = read_json(directory / "action-log.json")
    return directories, states, expected, actions


def collect_headless(root):
    states = {}
    directories = {}
    for scenario in HEADLESS_SCENARIOS:
        directory = find_scenario(root, scenario, ("ui-state.json", "actual.png"))
        directories[scenario] = directory
        states[scenario] = read_json(directory / "ui-state.json")
    return directories, states


def copy_selected_visuals(output, groups):
    copied = []
    for group, scenario, source_names, destination_name in VISUAL_SELECTION:
        directory = groups[group][scenario]
        source = next(
            (directory / name for name in source_names if (directory / name).exists()),
            None,
        )
        if source is None:
            raise RuntimeError(
                f"Missing selected visual for {group}/{scenario}: {source_names}"
            )
        destination = output / destination_name
        shutil.copy2(source, destination)
        copied.append(destination_name)
    return copied


def main():
    args = parse_args()
    roots = {
        "windows": Path(args.windows).resolve(),
        "macos": Path(args.macos).resolve(),
        "headless": Path(args.headless).resolve(),
    }
    output = Path(args.output).resolve() / "release-candidate"
    if output.exists():
        shutil.rmtree(output)
    output.mkdir(parents=True)

    windows_dirs, windows_states, windows_expected, windows_actions = collect_appium(
        roots["windows"]
    )
    macos_dirs, macos_states, macos_expected, macos_actions = collect_appium(
        roots["macos"]
    )
    headless_dirs, headless_states = collect_headless(roots["headless"])

    expected_document = {
        "candidateSha": args.candidate_sha,
        "appiumScenarios": list(APPIUM_SCENARIOS),
        "headlessScenarios": list(HEADLESS_SCENARIOS),
        "windows": windows_expected,
        "macos": macos_expected,
    }
    state_document = {
        "candidateSha": args.candidate_sha,
        "windows": windows_states,
        "macos": macos_states,
        "headless": headless_states,
    }
    action_document = {
        "candidateSha": args.candidate_sha,
        "windows": windows_actions,
        "macos": macos_actions,
    }

    (output / "expected.json").write_text(
        json.dumps(expected_document, indent=2, ensure_ascii=False) + "\n",
        encoding="utf-8",
    )
    (output / "ui-state.json").write_text(
        json.dumps(state_document, indent=2, ensure_ascii=False) + "\n",
        encoding="utf-8",
    )
    (output / "action-log.json").write_text(
        json.dumps(action_document, indent=2, ensure_ascii=False) + "\n",
        encoding="utf-8",
    )

    windows_json = windows_dirs["06-export"] / "actual-diagnostics.json"
    windows_sarif = windows_dirs["06-export"] / "actual-diagnostics.sarif"
    if not windows_json.exists() or not windows_sarif.exists():
        raise RuntimeError("Windows export evidence is missing JSON or SARIF diagnostics.")
    shutil.copy2(windows_json, output / "actual-diagnostics.json")
    shutil.copy2(windows_sarif, output / "actual-diagnostics.sarif")

    groups = {
        "windows": windows_dirs,
        "macos": macos_dirs,
        "headless": headless_dirs,
    }
    visuals = copy_selected_visuals(output, groups)

    manifest = {
        "version": 1,
        "scenario": "release-candidate",
        "candidateSha": args.candidate_sha,
        "visuals": visuals,
        "sources": {
            "windows": str(roots["windows"]),
            "macos": str(roots["macos"]),
            "headless": str(roots["headless"]),
        },
    }
    (output / "manifest.json").write_text(
        json.dumps(manifest, indent=2, ensure_ascii=False) + "\n",
        encoding="utf-8",
    )
    print(f"Built normalized UI AI evidence at {output}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
