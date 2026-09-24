#!/usr/bin/env python3
import argparse
import json
import os
import sys
import time
from pathlib import Path

from appium import webdriver
from appium.webdriver.common.appiumby import AppiumBy
from selenium.webdriver.common.keys import Keys
from selenium.webdriver.support.ui import WebDriverWait

from macos_bundle import validate_bundle


def parse_args():
    parser = argparse.ArgumentParser()
    parser.add_argument("--platform", choices=["windows", "macos"], required=True)
    parser.add_argument("--app", required=True)
    parser.add_argument("--fixture", required=True)
    parser.add_argument("--evidence", required=True)
    parser.add_argument("--server", default="http://127.0.0.1:4723")
    return parser.parse_args()


def load_expected(fixture: Path):
    manifest = json.loads((fixture / "expected-diagnostics.json").read_text(encoding="utf-8"))
    diagnostics = manifest["exactDiagnostics"]
    counts = {
        "danger": sum(item["severity"] == "danger" for item in diagnostics),
        "warning": sum(item["severity"] == "warning" for item in diagnostics),
        "attention": sum(item["severity"] == "attention" for item in diagnostics),
    }
    return diagnostics, counts


def create_driver(args):
    app = str(Path(args.app).resolve())
    if args.platform == "windows":
        from appium.options.windows import WindowsOptions

        options = WindowsOptions()
        options.platform_name = "Windows"
        options.automation_name = "NovaWindows"
        options.app = app
        options.set_capability("appium:newCommandTimeout", 180)
        return webdriver.Remote(args.server, options=options)

    from appium.options.mac import Mac2Options

    bundle_id = validate_bundle(Path(app))
    options = Mac2Options()
    options.platform_name = "mac"
    options.automation_name = "Mac2"
    options.app_path = app
    options.set_capability("appium:bundleId", bundle_id)
    options.set_capability("appium:showServerLogs", True)
    allowed_environment = (
        "PATH",
        "DOTNET_ROOT",
        "OOP_DESIGN_CHECKER_UI_AUTOMATION_EXPORT_DIR",
        "OOP_DESIGN_CHECKER_UI_AUTOMATION_ANALYSIS_DELAY_MS",
    )
    options.set_capability(
        "appium:environment",
        {key: os.environ[key] for key in allowed_environment if key in os.environ},
    )
    options.set_capability("appium:newCommandTimeout", 180)
    return webdriver.Remote(args.server, options=options)


def find(driver, automation_id):
    return driver.find_element(AppiumBy.ACCESSIBILITY_ID, automation_id)


def wait_until(driver, predicate, timeout=90):
    WebDriverWait(driver, timeout, poll_frequency=0.5).until(lambda _: predicate())


def text_of(driver, automation_id):
    element = find(driver, automation_id)
    return (
        element.text
        or element.get_attribute("Name")
        or element.get_attribute("Value.Value")
        or ""
    )


def replace_text(driver, automation_id, value):
    element = find(driver, automation_id)
    element.clear()
    element.send_keys(value)
    return element


def screenshot(driver, path: Path):
    path.parent.mkdir(parents=True, exist_ok=True)
    driver.save_screenshot(str(path))


def summary_matches(driver, counts):
    return all(
        str(counts[key]) in text_of(driver, control_id)
        for key, control_id in (
            ("danger", "DangerCount"),
            ("warning", "WarningCount"),
            ("attention", "AttentionCount"),
        )
    )


def configuration_editor(driver, platform):
    try:
        return find(driver, "ConfigurationEditor")
    except Exception:
        if platform == "windows":
            candidates = driver.find_elements(
                AppiumBy.XPATH,
                "//Edit[@ClassName='TextBox']",
            )
        else:
            candidates = driver.find_elements(
                AppiumBy.XPATH,
                "//XCUIElementTypeTextView",
            )
        if not candidates:
            raise
        return max(
            candidates,
            key=lambda element: element.rect["width"] * element.rect["height"],
        )


def select_rule(driver, platform, rule_id):
    replace_text(driver, "SearchFilter", rule_id)
    time.sleep(1)
    if platform == "macos":
        element = driver.find_element(
            AppiumBy.XPATH,
            f"//XCUIElementTypeStaticText[@value='{rule_id}']/ancestor::XCUIElementTypeCell[1]",
        )
    else:
        element = driver.find_element(AppiumBy.NAME, rule_id)
    element.click()
    wait_until(driver, lambda: rule_id in text_of(driver, "DetailRule"), timeout=20)


def write_scenario_state(driver, directory: Path, scenario, extra=None):
    directory.mkdir(parents=True, exist_ok=True)
    state = {
        "scenario": scenario,
        "window": driver.get_window_size(),
        "summary": {
            "danger": text_of(driver, "DangerCount"),
            "warning": text_of(driver, "WarningCount"),
            "attention": text_of(driver, "AttentionCount"),
        },
        "status": text_of(driver, "Status"),
        "selectedRule": text_of(driver, "DetailRule"),
        "selectedFile": text_of(driver, "DetailLocation"),
    }
    if extra:
        state.update(extra)
    (directory / "ui-state.json").write_text(
        json.dumps(state, indent=2, ensure_ascii=False), encoding="utf-8"
    )


def assert_exported_json(path: Path, expected_count):
    document = json.loads(path.read_text(encoding="utf-8-sig"))
    diagnostics = document.get("diagnostics", [])
    if document.get("version") != 1 or len(diagnostics) != expected_count:
        raise AssertionError(
            f"JSON export mismatch: version={document.get('version')} count={len(diagnostics)}"
        )


def exported_document_is_ready(path: Path, validator, expected_count):
    if not path.exists() or path.stat().st_size == 0:
        return False
    try:
        validator(path, expected_count)
        return True
    except (AssertionError, json.JSONDecodeError, OSError):
        return False


def assert_exported_sarif(path: Path, expected_count):
    document = json.loads(path.read_text(encoding="utf-8-sig"))
    runs = document.get("runs", [])
    results = runs[0].get("results", []) if len(runs) == 1 else []
    if document.get("version") != "2.1.0" or len(results) != expected_count:
        raise AssertionError(
            f"SARIF export mismatch: version={document.get('version')} count={len(results)}"
        )


def main():
    args = parse_args()
    fixture = Path(args.fixture).resolve()
    evidence = Path(args.evidence).resolve()
    evidence.mkdir(parents=True, exist_ok=True)
    export_dir = Path(
        os.environ.get(
            "OOP_DESIGN_CHECKER_UI_AUTOMATION_EXPORT_DIR",
            str(evidence / "export"),
        )
    ).resolve()
    export_dir.mkdir(parents=True, exist_ok=True)

    diagnostics, expected_counts = load_expected(fixture)
    actions = []
    scenario_actions = {}

    def record(scenario, action, result="pass", **details):
        item = {"scenario": scenario, "action": action, "result": result, **details}
        actions.append(item)
        scenario_actions.setdefault(scenario, []).append(item)

    def capture(driver, scenario, filename="after.png", extra=None):
        directory = evidence / scenario
        screenshot(driver, directory / filename)
        write_scenario_state(driver, directory, scenario, extra)

    for scenario, expected in {
        "01-cancel": {"status": "Analysis cancelled."},
        "02-fixture-analysis": {"count": len(diagnostics), "summary": expected_counts},
        "03-filter-detail": {"ruleId": "OOP106"},
        "04-language-resize": {"language": "en"},
        "05-config-editor": {"disabledRules": ["OOP105"]},
        "06-export": {"count": len(diagnostics), "formats": ["json", "sarif"]},
        "07-source-open-fallback": {"ruleId": "OOP106"},
        "08-error-state": {"diagnostics": 0},
    }.items():
        directory = evidence / scenario
        directory.mkdir(parents=True, exist_ok=True)
        (directory / "expected.json").write_text(
            json.dumps(expected, indent=2, ensure_ascii=False), encoding="utf-8"
        )

    driver = None
    moved_source = None
    try:
        driver = create_driver(args)
        record("02-fixture-analysis", "session-start", platform=args.platform)
        screenshot(driver, evidence / "02-fixture-analysis" / "before.png")

        replace_text(driver, "TargetPath", str(fixture))
        record("02-fixture-analysis", "set-target", value=str(fixture))

        find(driver, "AnalyzeButton").click()
        record("01-cancel", "analyze", result="started")
        wait_until(driver, lambda: find(driver, "CancelButton").is_enabled(), timeout=20)
        find(driver, "CancelButton").click()
        wait_until(
            driver,
            lambda: "cancelled" in text_of(driver, "Status").lower()
            or "キャンセル" in text_of(driver, "Status"),
            timeout=20,
        )
        record("01-cancel", "cancel")
        capture(driver, "01-cancel")
        wait_until(driver, lambda: find(driver, "AnalyzeButton").is_enabled(), timeout=20)

        find(driver, "AnalyzeButton").click()
        record("02-fixture-analysis", "analyze", result="started")
        wait_until(driver, lambda: summary_matches(driver, expected_counts), timeout=120)
        record("02-fixture-analysis", "analyze", result="pass")
        capture(
            driver,
            "02-fixture-analysis",
            extra={"target": str(fixture), "platform": args.platform},
        )

        select_rule(driver, args.platform, "OOP106")
        record("03-filter-detail", "filter-select", ruleId="OOP106")
        capture(driver, "03-filter-detail")

        language = find(driver, "LanguageSelector")
        language.click()
        if args.platform == "macos":
            english = driver.find_element(
                AppiumBy.XPATH,
                "//XCUIElementTypeMenuItem[@title='English']",
            )
            rect = english.rect
            driver.execute_script(
                "macos: click",
                {
                    "x": rect["x"] + rect["width"] / 2,
                    "y": rect["y"] + rect["height"] / 2,
                },
            )
            wait_until(
                driver,
                lambda: find(driver, "LanguageSelector").get_attribute("value") == "English",
                timeout=20,
            )
        else:
            language.send_keys("English")
            language.send_keys(Keys.ENTER)
        wait_until(
            driver,
            lambda: "Rule:" in text_of(driver, "DetailRule"),
            timeout=20,
        )
        record("04-language-resize", "language", value="en")

        if args.platform == "macos":
            resize_value = "native-resize-covered-by-headless"
        else:
            driver.set_window_size(900, 600)
            resize_value = "900x600"
        time.sleep(1)
        record("04-language-resize", "resize", value=resize_value)
        capture(
            driver,
            "04-language-resize",
            "resized.png",
            {"language": "en", "resize": resize_value},
        )

        config_path = evidence / "05-config-editor" / "oop-design-checker.json"
        config_path.write_text(
            '{"disabledRules":["OOP105"]}',
            encoding="utf-8",
        )
        replace_text(driver, "ConfigurationPath", str(config_path))
        main_window = driver.current_window_handle if args.platform == "windows" else None
        known_windows = set(driver.window_handles) if args.platform == "windows" else set()
        find(driver, "EditConfigurationButton").click()

        def switch_to_configuration_editor():
            if args.platform == "windows":
                for handle in driver.window_handles:
                    if handle in known_windows:
                        continue
                    try:
                        driver.switch_to.window(handle)
                        configuration_editor(driver, args.platform)
                        return True
                    except Exception:
                        continue

            try:
                configuration_editor(driver, args.platform)
                return True
            except Exception:
                if args.platform == "windows" and main_window is not None:
                    driver.switch_to.window(main_window)
                return False

        wait_until(driver, switch_to_configuration_editor, timeout=20)
        editor = configuration_editor(driver, args.platform)
        editor.send_keys(Keys.END)
        editor.send_keys(" ")
        find(driver, "ValidateConfigurationButton").click()
        wait_until(
            driver,
            lambda: "valid" in text_of(driver, "ConfigurationEditorStatus").lower(),
            timeout=20,
        )
        find(driver, "SaveConfigurationButton").click()
        if args.platform == "windows":
            wait_until(
                driver,
                lambda: main_window in driver.window_handles,
                timeout=20,
            )
            driver.switch_to.window(main_window)

        wait_until(
            driver,
            lambda: config_path.exists()
            and config_path.read_text(encoding="utf-8").endswith(" ")
            and "OOP105" in config_path.read_text(encoding="utf-8"),
            timeout=20,
        )
        wait_until(driver, lambda: find(driver, "AnalyzeButton").is_enabled(), timeout=20)
        record(
            "05-config-editor",
            "edit-validate-save",
            path=str(config_path),
            status=text_of(driver, "Status"),
        )

        find(driver, "AnalyzeButton").click()
        configured_counts = dict(expected_counts)
        configured_counts["warning"] -= 1
        wait_until(driver, lambda: summary_matches(driver, configured_counts), timeout=120)
        record("05-config-editor", "reanalyze", disabledRule="OOP105")
        capture(
            driver,
            "05-config-editor",
            extra={"configurationPath": str(config_path), "summary": configured_counts},
        )

        find(driver, "ClearConfigurationButton").click()
        find(driver, "AnalyzeButton").click()
        wait_until(driver, lambda: summary_matches(driver, expected_counts), timeout=120)
        record("06-export", "reanalyze-defaults")

        json_path = export_dir / "actual-diagnostics.json"
        sarif_path = export_dir / "actual-diagnostics.sarif"
        for path in (json_path, sarif_path):
            path.unlink(missing_ok=True)

        find(driver, "ExportJsonButton").click()
        wait_until(
            driver,
            lambda: exported_document_is_ready(
                json_path, assert_exported_json, len(diagnostics)
            ),
            timeout=20,
        )
        record("06-export", "export-json", path=str(json_path))

        find(driver, "ExportSarifButton").click()
        wait_until(
            driver,
            lambda: exported_document_is_ready(
                sarif_path, assert_exported_sarif, len(diagnostics)
            ),
            timeout=20,
        )
        record("06-export", "export-sarif", path=str(sarif_path))

        export_scenario = evidence / "06-export"
        (export_scenario / "actual-diagnostics.json").write_bytes(json_path.read_bytes())
        (export_scenario / "actual-diagnostics.sarif").write_bytes(sarif_path.read_bytes())
        capture(driver, "06-export")

        select_rule(driver, args.platform, "OOP106")
        location_text = text_of(driver, "DetailLocation")
        source_candidates = [
            fixture / item["file"]
            for item in diagnostics
            if item["ruleId"] == "OOP106" and Path(item["file"]).name in location_text
        ]
        if not source_candidates:
            raise AssertionError(f"Could not resolve selected OOP106 source from: {location_text}")
        source_path = source_candidates[0]
        backup_path = source_path.with_name(source_path.name + ".ui-appium-backup")
        source_path.rename(backup_path)
        moved_source = (source_path, backup_path)
        try:
            find(driver, "OpenSourceButton").click()
            wait_until(
                driver,
                lambda: "not found" in text_of(driver, "Status").lower(),
                timeout=20,
            )
            record("07-source-open-fallback", "open-missing-source", file=str(source_path))
            capture(driver, "07-source-open-fallback")
        finally:
            if backup_path.exists():
                backup_path.rename(source_path)
            moved_source = None

        missing_target = evidence / "08-error-state" / "missing-target"
        replace_text(driver, "TargetPath", str(missing_target))
        find(driver, "AnalyzeButton").click()
        wait_until(driver, lambda: find(driver, "AnalyzeButton").is_enabled(), timeout=120)
        wait_until(
            driver,
            lambda: summary_matches(
                driver, {"danger": 0, "warning": 0, "attention": 0}
            ),
            timeout=20,
        )
        error_status = text_of(driver, "Status")
        if not error_status.strip():
            raise AssertionError("Error state did not expose a visible status message.")
        record("08-error-state", "analyze-invalid-target", status=error_status)
        capture(driver, "08-error-state", extra={"target": str(missing_target)})

        return 0
    except Exception as exc:
        record(
            "failure",
            "exception",
            result="fail",
            errorType=type(exc).__name__,
            error=str(exc),
        )
        if driver is not None:
            try:
                screenshot(driver, evidence / "failure.png")
                (evidence / "page-source.xml").write_text(driver.page_source, encoding="utf-8")
            except Exception:
                pass
        print(f"Appium UI E2E failed: {exc}", file=sys.stderr)
        return 1
    finally:
        if moved_source is not None:
            source_path, backup_path = moved_source
            if backup_path.exists():
                backup_path.rename(source_path)

        (evidence / "action-log.json").write_text(
            json.dumps(actions, indent=2, ensure_ascii=False), encoding="utf-8"
        )
        for scenario, items in scenario_actions.items():
            directory = evidence / scenario
            directory.mkdir(parents=True, exist_ok=True)
            (directory / "action-log.json").write_text(
                json.dumps(items, indent=2, ensure_ascii=False), encoding="utf-8"
            )

        if driver is not None:
            try:
                driver.quit()
            except Exception:
                pass


if __name__ == "__main__":
    raise SystemExit(main())
