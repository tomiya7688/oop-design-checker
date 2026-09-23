#!/usr/bin/env python3
import argparse
import json
import os
import shutil
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
    parser.add_argument("--automation-root")
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
    options.set_capability(
        "appium:environment",
        {
            key: os.environ[key]
            for key in ("PATH", "DOTNET_ROOT", "OOP_DESIGN_CHECKER_UI_E2E_EXPORT_DIR")
            if key in os.environ
        },
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
        or element.get_attribute("value")
        or ""
    )


def set_text(driver, automation_id, value):
    element = find(driver, automation_id)
    element.click()
    element.clear()
    element.send_keys(value)
    return element


def screenshot(driver, path: Path):
    path.parent.mkdir(parents=True, exist_ok=True)
    driver.save_screenshot(str(path))


def scenario_directory(evidence: Path, scenario: str) -> Path:
    directory = evidence / scenario
    directory.mkdir(parents=True, exist_ok=True)
    return directory


def write_json(path: Path, value):
    path.write_text(json.dumps(value, indent=2, ensure_ascii=False), encoding="utf-8")


def summary_text(driver):
    return {
        "danger": text_of(driver, "DangerCount"),
        "warning": text_of(driver, "WarningCount"),
        "attention": text_of(driver, "AttentionCount"),
    }


def summary_matches(driver, counts):
    return all(
        str(counts[key]) in text_of(driver, control_id)
        for key, control_id in (
            ("danger", "DangerCount"),
            ("warning", "WarningCount"),
            ("attention", "AttentionCount"),
        )
    )


def write_ui_state(driver, directory: Path, args, target, language, extra=None):
    state = {
        "platform": args.platform,
        "window": driver.get_window_size(),
        "target": str(target),
        "summary": summary_text(driver),
        "selectedRule": text_of(driver, "DetailRule"),
        "status": text_of(driver, "Status"),
        "language": language,
    }
    if extra:
        state.update(extra)
    write_json(directory / "ui-state.json", state)


def select_oop106(driver, platform):
    search = set_text(driver, "SearchFilter", "OOP106")
    del search
    time.sleep(1)
    if platform == "macos":
        row = driver.find_element(
            AppiumBy.XPATH,
            "//XCUIElementTypeStaticText[@value='OOP106']/ancestor::XCUIElementTypeCell[1]",
        )
    else:
        row = driver.find_element(AppiumBy.NAME, "OOP106")
    row.click()
    wait_until(driver, lambda: "OOP106" in text_of(driver, "DetailRule"), timeout=20)


def run_analysis(driver, target: Path, counts, configuration: Path | None = None):
    set_text(driver, "TargetPath", str(target))
    configuration_box = find(driver, "ConfigurationPath")
    configuration_box.click()
    configuration_box.clear()
    if configuration is not None:
        configuration_box.send_keys(str(configuration))
    find(driver, "AnalyzeButton").click()
    wait_until(driver, lambda: summary_matches(driver, counts))


def prepare_cancel_fixture(fixture: Path, root: Path):
    if root.exists():
        shutil.rmtree(root)
    root.mkdir(parents=True)
    for index in range(16):
        shutil.copytree(fixture, root / f"project-{index:02d}")


def main():
    args = parse_args()
    fixture = Path(args.fixture).resolve()
    evidence = Path(args.evidence).resolve()
    automation_root = (
        Path(args.automation_root).resolve()
        if args.automation_root
        else (evidence.parent / "ui-appium-automation").resolve()
    )
    evidence.mkdir(parents=True, exist_ok=True)
    automation_root.mkdir(parents=True, exist_ok=True)

    configured_fixture = automation_root / "configured-fixture"
    if configured_fixture.exists():
        shutil.rmtree(configured_fixture)
    shutil.copytree(fixture, configured_fixture)
    configured_fixture_config = configured_fixture / "oop-design-checker.json"
    configured_fixture_config.write_text(
        '{"disabledRules":["OOP105"]}\n',
        encoding="utf-8",
    )

    diagnostics, expected_counts = load_expected(fixture)
    oop105_count = sum(item["ruleId"] == "OOP105" for item in diagnostics)
    expected_configured_counts = dict(expected_counts)
    expected_configured_counts["warning"] -= oop105_count

    actions = {}
    driver = None
    source_backup = None
    source_path = None

    def log(scenario, action, result="pass", **details):
        actions.setdefault(scenario, []).append(
            {"action": action, "result": result, **details}
        )

    try:
        driver = create_driver(args)

        analysis = scenario_directory(evidence, "fixture-analysis")
        write_json(
            analysis / "expected.json",
            {"count": len(diagnostics), "summary": expected_counts},
        )
        screenshot(driver, analysis / "before.png")
        log("fixture-analysis", "session-start")
        set_text(driver, "TargetPath", str(fixture))
        log("fixture-analysis", "set-target", value=str(fixture))
        find(driver, "AnalyzeButton").click()
        log("fixture-analysis", "analyze", result="started")
        wait_until(driver, lambda: summary_matches(driver, expected_counts))
        actions["fixture-analysis"][-1]["result"] = "pass"
        screenshot(driver, analysis / "after.png")
        write_ui_state(driver, analysis, args, fixture, "ja")
        log("fixture-analysis", "summary-match", summary=expected_counts)

        interaction = scenario_directory(evidence, "filter-detail-language-resize")
        write_json(
            interaction / "expected.json",
            {"rule": "OOP106", "language": "en", "resize": "supported-native-resize"},
        )
        screenshot(driver, interaction / "before.png")
        select_oop106(driver, args.platform)
        log("filter-detail-language-resize", "filter", value="OOP106")
        log("filter-detail-language-resize", "select-detail", rule="OOP106")

        language = find(driver, "LanguageSelector")
        language.click()
        try:
            wait_until(
                driver,
                lambda: driver.find_element(AppiumBy.NAME, "English").is_displayed(),
                timeout=10,
            )
            driver.find_element(AppiumBy.NAME, "English").click()
        except Exception:
            language.send_keys("English")
            language.send_keys(Keys.ENTER)
        wait_until(
            driver,
            lambda: "Analyze" in text_of(driver, "AnalyzeButton"),
            timeout=20,
        )
        log("filter-detail-language-resize", "language", value="en")

        if args.platform == "macos":
            driver.maximize_window()
            resize_value = "maximize"
        else:
            driver.set_window_size(900, 600)
            resize_value = "900x600"
        time.sleep(1)
        screenshot(driver, interaction / "resized.png")
        screenshot(driver, interaction / "after.png")
        log("filter-detail-language-resize", "resize", value=resize_value)
        write_ui_state(
            driver,
            interaction,
            args,
            fixture,
            "en",
            {"search": "OOP106", "resize": resize_value},
        )

        configuration = scenario_directory(evidence, "configuration")
        write_json(
            configuration / "expected.json",
            {
                "config": "oop-design-checker.json",
                "disabledRule": "OOP105",
                "summary": expected_configured_counts,
            },
        )
        screenshot(driver, configuration / "before.png")
        set_text(driver, "TargetPath", str(configured_fixture))
        config_box = find(driver, "ConfigurationPath")
        config_box.click()
        config_box.clear()
        find(driver, "AnalyzeButton").click()
        wait_until(driver, lambda: summary_matches(driver, expected_configured_counts))
        wait_until(
            driver,
            lambda: "oop-design-checker.json" in text_of(driver, "ConfigurationSummary"),
            timeout=20,
        )
        log(
            "configuration",
            "analyze-with-auto-discovered-config",
            path=str(configured_fixture_config),
            disabledRule="OOP105",
            summary=expected_configured_counts,
        )
        screenshot(driver, configuration / "after.png")
        write_ui_state(driver, configuration, args, configured_fixture, "en")

        set_text(driver, "TargetPath", str(fixture))
        find(driver, "AnalyzeButton").click()
        wait_until(driver, lambda: summary_matches(driver, expected_counts))
        log("configuration", "restore-default-fixture", summary=expected_counts)

        export = scenario_directory(evidence, "export")
        export_root = Path(
            os.environ.get(
                "OOP_DESIGN_CHECKER_UI_E2E_EXPORT_DIR",
                str(automation_root / "exports"),
            )
        ).resolve()
        export_root.mkdir(parents=True, exist_ok=True)
        json_export = export_root / "oop-design-checker-results.json"
        sarif_export = export_root / "oop-design-checker-results.sarif"
        for candidate in (json_export, sarif_export):
            if candidate.exists():
                candidate.unlink()
        write_json(
            export / "expected.json",
            {"jsonDiagnostics": len(diagnostics), "sarifVersion": "2.1.0"},
        )
        screenshot(driver, export / "before.png")
        find(driver, "ExportJsonButton").click()
        wait_until(driver, json_export.exists, timeout=30)
        exported_json = json.loads(json_export.read_text(encoding="utf-8-sig"))
        if len(exported_json.get("diagnostics", [])) != len(diagnostics):
            raise AssertionError("JSON export diagnostic count does not match release fixture")
        shutil.copy2(json_export, export / "actual-diagnostics.json")
        log("export", "json-export", path=str(json_export))

        find(driver, "ExportSarifButton").click()
        wait_until(driver, sarif_export.exists, timeout=30)
        exported_sarif = json.loads(sarif_export.read_text(encoding="utf-8-sig"))
        if exported_sarif.get("version") != "2.1.0":
            raise AssertionError("SARIF export did not produce SARIF 2.1.0")
        shutil.copy2(sarif_export, export / "actual-diagnostics.sarif")
        log("export", "sarif-export", path=str(sarif_export))
        screenshot(driver, export / "after.png")
        write_ui_state(driver, export, args, fixture, "en")

        source = scenario_directory(evidence, "source-open-fallback")
        write_json(source / "expected.json", {"statusContains": "not found"})
        screenshot(driver, source / "before.png")
        select_oop106(driver, args.platform)
        oop106 = next(item for item in diagnostics if item["ruleId"] == "OOP106")
        source_path = fixture / oop106["file"]
        source_backup = source_path.with_suffix(source_path.suffix + ".appium-bak")
        if source_backup.exists():
            source_backup.unlink()
        source_path.rename(source_backup)
        find(driver, "OpenSourceButton").click()
        wait_until(
            driver,
            lambda: "not found" in text_of(driver, "Status").lower(),
            timeout=20,
        )
        log("source-open-fallback", "open-missing-source", file=str(source_path))
        screenshot(driver, source / "after.png")
        write_ui_state(driver, source, args, fixture, "en")
        source_backup.rename(source_path)
        source_backup = None

        cancel = scenario_directory(evidence, "cancel")
        cancel_root = automation_root / "cancel-fixture"
        prepare_cancel_fixture(fixture, cancel_root)
        write_json(cancel / "expected.json", {"statusContains": "cancel"})
        screenshot(driver, cancel / "before.png")
        set_text(driver, "TargetPath", str(cancel_root))
        configuration_box = find(driver, "ConfigurationPath")
        configuration_box.click()
        configuration_box.clear()
        find(driver, "AnalyzeButton").click()
        wait_until(driver, lambda: find(driver, "CancelButton").is_enabled(), timeout=20)
        find(driver, "CancelButton").click()
        wait_until(
            driver,
            lambda: "cancel" in text_of(driver, "Status").lower(),
            timeout=30,
        )
        log("cancel", "cancel-analysis")
        screenshot(driver, cancel / "after.png")
        write_ui_state(driver, cancel, args, cancel_root, "en")

        error = scenario_directory(evidence, "invalid-target-error")
        missing_target = automation_root / "missing-target"
        write_json(error / "expected.json", {"summary": {"danger": 0, "warning": 0, "attention": 0}})
        screenshot(driver, error / "before.png")
        set_text(driver, "TargetPath", str(missing_target))
        find(driver, "AnalyzeButton").click()
        wait_until(
            driver,
            lambda: summary_matches(
                driver,
                {"danger": 0, "warning": 0, "attention": 0},
            )
            and text_of(driver, "Status").lower() not in {"ready", "analyzing..."},
            timeout=30,
        )
        log("invalid-target-error", "analyze-missing-target", target=str(missing_target))
        screenshot(driver, error / "after.png")
        write_ui_state(driver, error, args, missing_target, "en")

        return 0
    except Exception as exc:
        failure = scenario_directory(evidence, "failure")
        log(
            "failure",
            "failure",
            result="fail",
            errorType=type(exc).__name__,
            error=str(exc),
        )
        if driver is not None:
            try:
                screenshot(driver, failure / "failure.png")
                (failure / "page-source.xml").write_text(driver.page_source, encoding="utf-8")
            except Exception:
                pass
        print(f"Appium UI E2E failed: {exc}", file=sys.stderr)
        return 1
    finally:
        if source_backup is not None and source_path is not None and source_backup.exists():
            source_backup.rename(source_path)
        for scenario, scenario_actions in actions.items():
            directory = scenario_directory(evidence, scenario)
            write_json(directory / "action-log.json", scenario_actions)
        if driver is not None:
            try:
                driver.quit()
            except Exception:
                pass


if __name__ == "__main__":
    raise SystemExit(main())
