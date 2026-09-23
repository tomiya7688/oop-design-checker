#!/usr/bin/env python3
import argparse
import json
import os
import shutil
import sys
import tempfile
import time
from pathlib import Path

from appium import webdriver
from appium.webdriver.common.appiumby import AppiumBy
from selenium.common.exceptions import WebDriverException
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
        export_directory = os.environ["OOP_DESIGN_CHECKER_UI_AUTOMATION_EXPORT_DIR"]
        options.set_capability(
            "appium:appArguments",
            f'--ui-automation-export-dir "{export_directory}"',
        )
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
        "appium:arguments",
        [
            "--ui-automation-export-dir",
            os.environ["OOP_DESIGN_CHECKER_UI_AUTOMATION_EXPORT_DIR"],
        ],
    )
    allowed_environment = (
        "PATH",
        "DOTNET_ROOT",
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


def element_value(element):
    text = element.text
    if text:
        return text
    for attribute in ("value", "Name", "Value.Value"):
        try:
            value = element.get_attribute(attribute)
        except WebDriverException:
            continue
        if value:
            return value
    return ""


def text_of(driver, automation_id):
    return element_value(find(driver, automation_id))


def set_text(driver, automation_id, value):
    element = find(driver, automation_id)
    element.clear()
    element.send_keys(str(value))


def scenario_dir(evidence: Path, scenario: str):
    directory = evidence / scenario
    directory.mkdir(parents=True, exist_ok=True)
    return directory


def screenshot(driver, evidence: Path, scenario: str, name="after.png"):
    path = scenario_dir(evidence, scenario) / name
    driver.save_screenshot(str(path))
    return path


def write_json(path: Path, value):
    path.write_text(json.dumps(value, indent=2, ensure_ascii=False), encoding="utf-8")


def ui_state(driver, target=None, extra=None):
    state = {
        "window": driver.get_window_size(),
        "target": str(target) if target else None,
        "summary": {
            "danger": text_of(driver, "DangerCount"),
            "warning": text_of(driver, "WarningCount"),
            "attention": text_of(driver, "AttentionCount"),
        },
        "selectedRule": text_of(driver, "DetailRule"),
        "selectedFile": text_of(driver, "DetailLocation"),
        "status": text_of(driver, "Status"),
        "search": element_value(find(driver, "SearchFilter")),
    }
    if extra:
        state.update(extra)
    return state


def select_rule(driver, platform, rule_id):
    if platform == "macos":
        element = driver.find_element(
            AppiumBy.XPATH,
            f"//XCUIElementTypeStaticText[@value='{rule_id}']/ancestor::XCUIElementTypeCell[1]",
        )
    else:
        element = driver.find_element(AppiumBy.NAME, rule_id)
    element.click()
    wait_until(driver, lambda: rule_id in text_of(driver, "DetailRule"), timeout=20)


def select_english(driver, platform):
    language = find(driver, "LanguageSelector")
    language.click()
    if platform == "macos":
        driver.execute_script(
            "macos: keys",
            {
                "elementId": language.id,
                "keys": ["XCUIKeyboardKeyDownArrow", "XCUIKeyboardKeyReturn"],
            },
        )
    else:
        language.send_keys("English")
        language.send_keys(Keys.ENTER)

    wait_until(
        driver,
        lambda: "English" in element_value(find(driver, "LanguageSelector"))
        and "Analyze" in text_of(driver, "AnalyzeButton"),
        timeout=20,
    )


def make_cancel_project(root: Path):
    project = root / "cancel-project"
    project.mkdir(parents=True)
    (project / "CancelProject.csproj").write_text(
        """<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
  </PropertyGroup>
</Project>
""",
        encoding="utf-8",
    )
    method_body = "\n".join(f"        total += {index};" for index in range(80))
    for index in range(220):
        (project / f"Sample{index:03d}.cs").write_text(
            f"""namespace CancelFixture;

internal sealed class Sample{index:03d}
{{
    public int Run()
    {{
        var total = 0;
{method_body}
        return total;
    }}
}}
""",
            encoding="utf-8",
        )
    return project


def validate_exports(export_directory: Path, expected_count: int):
    json_path = export_directory / "oop-design-checker-results.json"
    sarif_path = export_directory / "oop-design-checker-results.sarif"
    if not json_path.is_file() or not sarif_path.is_file():
        raise AssertionError("Expected JSON and SARIF export files were not created.")

    json_document = json.loads(json_path.read_text(encoding="utf-8-sig"))
    if len(json_document["diagnostics"]) != expected_count:
        raise AssertionError(
            f"JSON export count mismatch: expected {expected_count}, "
            f"found {len(json_document['diagnostics'])}."
        )

    sarif_document = json.loads(sarif_path.read_text(encoding="utf-8-sig"))
    results = sarif_document["runs"][0]["results"]
    if len(results) != expected_count:
        raise AssertionError(
            f"SARIF export count mismatch: expected {expected_count}, found {len(results)}."
        )
    return json_path, sarif_path


def main():
    args = parse_args()
    source_fixture = Path(args.fixture).resolve()
    evidence = Path(args.evidence).resolve()
    evidence.mkdir(parents=True, exist_ok=True)
    export_directory = Path(
        os.environ["OOP_DESIGN_CHECKER_UI_AUTOMATION_EXPORT_DIR"]
    ).resolve()
    export_directory.mkdir(parents=True, exist_ok=True)

    diagnostics, expected_counts = load_expected(source_fixture)
    actions = []
    driver = None

    with tempfile.TemporaryDirectory(prefix="oop-ui-appium-") as temporary:
        workspace = Path(temporary)
        fixture = workspace / "release-validation-csharp"
        shutil.copytree(source_fixture, fixture)
        cancel_project = make_cancel_project(workspace)

        fixture_scenario = scenario_dir(evidence, "fixture-analysis")
        write_json(
            fixture_scenario / "expected.json",
            {
                "count": len(diagnostics),
                "summary": expected_counts,
                "diagnostics": diagnostics,
            },
        )

        try:
            driver = create_driver(args)
            actions.append({"scenario": "startup", "action": "session-start", "result": "pass"})
            screenshot(driver, evidence, "fixture-analysis", "before.png")

            set_text(driver, "TargetPath", fixture)
            actions.append(
                {
                    "scenario": "fixture-analysis",
                    "action": "set-target",
                    "value": str(fixture),
                    "result": "pass",
                }
            )

            find(driver, "AnalyzeButton").click()
            actions.append(
                {"scenario": "fixture-analysis", "action": "analyze", "result": "started"}
            )

            def summary_matches():
                return all(
                    str(expected_counts[key]) in text_of(driver, control_id)
                    for key, control_id in (
                        ("danger", "DangerCount"),
                        ("warning", "WarningCount"),
                        ("attention", "AttentionCount"),
                    )
                )

            wait_until(driver, summary_matches)
            actions[-1]["result"] = "pass"
            screenshot(driver, evidence, "fixture-analysis")
            write_json(
                fixture_scenario / "ui-state.json",
                ui_state(driver, fixture, {"language": "ja"}),
            )

            set_text(driver, "SearchFilter", "OOP106")
            time.sleep(1)
            select_rule(driver, args.platform, "OOP106")
            actions.append(
                {
                    "scenario": "filter-detail",
                    "action": "filter-and-select",
                    "rule": "OOP106",
                    "result": "pass",
                }
            )
            screenshot(driver, evidence, "filter-detail")
            write_json(
                scenario_dir(evidence, "filter-detail") / "ui-state.json",
                ui_state(driver, fixture, {"selectedRule": "OOP106"}),
            )

            select_english(driver, args.platform)
            actions.append(
                {"scenario": "language-resize", "action": "language", "value": "en", "result": "pass"}
            )
            screenshot(driver, evidence, "language-resize", "before.png")

            if args.platform == "macos":
                driver.maximize_window()
                resize_value = "maximize"
            else:
                driver.set_window_size(900, 600)
                resize_value = "900x600"
            time.sleep(1)
            screenshot(driver, evidence, "language-resize", "resized.png")
            actions.append(
                {
                    "scenario": "language-resize",
                    "action": "resize",
                    "value": resize_value,
                    "result": "pass",
                }
            )
            write_json(
                scenario_dir(evidence, "language-resize") / "ui-state.json",
                ui_state(driver, fixture, {"language": "en", "resize": resize_value}),
            )

            for automation_id in ("ExportJsonButton", "ExportSarifButton"):
                find(driver, automation_id).click()
                wait_until(
                    driver,
                    lambda: "Exported diagnostics to" in text_of(driver, "Status"),
                    timeout=30,
                )
            json_export, sarif_export = validate_exports(export_directory, len(diagnostics))
            shutil.copy2(json_export, fixture_scenario / "actual-diagnostics.json")
            export_scenario = scenario_dir(evidence, "export")
            shutil.copy2(json_export, export_scenario / "actual-diagnostics.json")
            shutil.copy2(sarif_export, export_scenario / "actual-diagnostics.sarif")
            write_json(
                export_scenario / "expected.json",
                {"formats": ["json", "sarif"], "diagnosticCount": len(diagnostics)},
            )
            screenshot(driver, evidence, "export")
            actions.append(
                {
                    "scenario": "export",
                    "action": "json-and-sarif",
                    "result": "pass",
                }
            )

            config_path = workspace / "oop-design-checker.ui-appium.json"
            config_text = '{"disabledRules":["OOP105"],"failureThreshold":"warning"}'
            config_path.write_text(config_text, encoding="utf-8")
            set_text(driver, "ConfigurationPath", config_path)
            find(driver, "EditConfigurationButton").click()
            wait_until(driver, lambda: find(driver, "ConfigurationEditor").is_displayed(), timeout=20)
            set_text(driver, "ConfigurationEditor", config_text)
            find(driver, "ValidateConfigurationButton").click()
            wait_until(
                driver,
                lambda: "valid" in text_of(driver, "ConfigurationEditorStatus").lower(),
                timeout=20,
            )
            find(driver, "SaveConfigurationButton").click()
            wait_until(
                driver,
                lambda: "Configuration saved" in text_of(driver, "Status"),
                timeout=20,
            )
            find(driver, "AnalyzeButton").click()
            wait_until(
                driver,
                lambda: "Disabled: 1" in text_of(driver, "ConfigurationSummary"),
                timeout=90,
            )
            screenshot(driver, evidence, "configuration")
            write_json(
                scenario_dir(evidence, "configuration") / "ui-state.json",
                ui_state(driver, fixture, {"configurationPath": str(config_path)}),
            )
            actions.append(
                {"scenario": "configuration", "action": "edit-validate-save-apply", "result": "pass"}
            )

            set_text(driver, "SearchFilter", "OOP106")
            time.sleep(1)
            select_rule(driver, args.platform, "OOP106")
            for relative in (
                Path("Positive") / "Encapsulation.cs",
                Path("Positive") / "ObjectDesign.cs",
            ):
                source = fixture / relative
                if source.exists():
                    source.unlink()
            find(driver, "OpenSourceButton").click()
            wait_until(
                driver,
                lambda: "not found" in text_of(driver, "Status").lower()
                and "copied" in text_of(driver, "Status").lower(),
                timeout=20,
            )
            screenshot(driver, evidence, "source-open")
            write_json(
                scenario_dir(evidence, "source-open") / "ui-state.json",
                ui_state(driver, fixture, {"expectedFallback": "source-not-found"}),
            )
            actions.append(
                {"scenario": "source-open", "action": "missing-source-fallback", "result": "pass"}
            )

            set_text(driver, "ConfigurationPath", "")
            set_text(driver, "SearchFilter", "")
            set_text(driver, "TargetPath", cancel_project)
            find(driver, "AnalyzeButton").click()
            wait_until(driver, lambda: find(driver, "CancelButton").is_enabled(), timeout=10)
            find(driver, "CancelButton").click()
            wait_until(
                driver,
                lambda: "cancel" in text_of(driver, "Status").lower(),
                timeout=30,
            )
            screenshot(driver, evidence, "cancel")
            write_json(
                scenario_dir(evidence, "cancel") / "ui-state.json",
                ui_state(driver, cancel_project, {"expected": "cancelled"}),
            )
            actions.append({"scenario": "cancel", "action": "cancel-analysis", "result": "pass"})

            missing_target = workspace / "missing-target"
            set_text(driver, "TargetPath", missing_target)
            find(driver, "AnalyzeButton").click()
            wait_until(
                driver,
                lambda: find(driver, "AnalyzeButton").is_enabled()
                and text_of(driver, "Status") not in ("Analyzing...", "Cancelling...", "Ready"),
                timeout=30,
            )
            error_status = text_of(driver, "Status")
            if "Completed:" in error_status or "cancel" in error_status.lower():
                raise AssertionError(f"Invalid target did not render an error state: {error_status}")
            screenshot(driver, evidence, "error")
            write_json(
                scenario_dir(evidence, "error") / "ui-state.json",
                ui_state(driver, missing_target, {"error": error_status}),
            )
            actions.append(
                {
                    "scenario": "error",
                    "action": "invalid-target",
                    "status": error_status,
                    "result": "pass",
                }
            )

            write_json(
                evidence / "ui-state.json",
                {
                    "platform": args.platform,
                    "scenarioIds": [
                        "fixture-analysis",
                        "filter-detail",
                        "language-resize",
                        "export",
                        "configuration",
                        "source-open",
                        "cancel",
                        "error",
                    ],
                    "final": ui_state(driver, missing_target, {"language": "en"}),
                },
            )
            return 0
        except Exception as exc:
            actions.append(
                {
                    "scenario": "failure",
                    "action": "failure",
                    "result": "fail",
                    "errorType": type(exc).__name__,
                    "error": str(exc),
                }
            )
            if driver is not None:
                try:
                    screenshot(driver, evidence, "failure", "failure.png")
                    (scenario_dir(evidence, "failure") / "page-source.xml").write_text(
                        driver.page_source, encoding="utf-8"
                    )
                except Exception:
                    pass
            print(f"Appium UI E2E failed: {exc}", file=sys.stderr)
            return 1
        finally:
            write_json(evidence / "action-log.json", actions)
            if driver is not None:
                try:
                    driver.quit()
                except Exception:
                    pass


if __name__ == "__main__":
    raise SystemExit(main())
