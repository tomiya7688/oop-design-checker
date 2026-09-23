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
    # XCTest launches via LaunchServices; explicitly retain SDK discovery settings.
    # Do not forward the full CI environment (which may contain secrets).
    options.set_capability(
        "appium:environment",
        {key: os.environ[key] for key in ("PATH", "DOTNET_ROOT") if key in os.environ},
    )
    options.set_capability("appium:newCommandTimeout", 180)
    return webdriver.Remote(args.server, options=options)


def find(driver, automation_id):
    return driver.find_element(AppiumBy.ACCESSIBILITY_ID, automation_id)


def wait_until(driver, predicate, timeout=90):
    WebDriverWait(driver, timeout, poll_frequency=0.5).until(lambda _: predicate())


def text_of(driver, automation_id):
    element = find(driver, automation_id)
    return element.text or element.get_attribute("Name") or element.get_attribute("Value.Value") or ""


def screenshot(driver, path: Path):
    path.parent.mkdir(parents=True, exist_ok=True)
    driver.save_screenshot(str(path))


def main():
    args = parse_args()
    fixture = Path(args.fixture).resolve()
    evidence = Path(args.evidence).resolve()
    evidence.mkdir(parents=True, exist_ok=True)

    diagnostics, expected_counts = load_expected(fixture)
    (evidence / "expected.json").write_text(
        json.dumps({"count": len(diagnostics), "summary": expected_counts}, indent=2),
        encoding="utf-8",
    )

    actions = []
    driver = None
    try:
        driver = create_driver(args)
        actions.append({"action": "session-start", "result": "pass"})
        screenshot(driver, evidence / "before.png")

        target = find(driver, "TargetPath")
        target.clear()
        target.send_keys(str(fixture))
        actions.append({"action": "set-target", "value": str(fixture), "result": "pass"})

        find(driver, "AnalyzeButton").click()
        actions.append({"action": "analyze", "result": "started"})

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
        screenshot(driver, evidence / "after.png")

        search = find(driver, "SearchFilter")
        search.clear()
        search.send_keys("OOP106")
        time.sleep(1)
        actions.append({"action": "filter", "value": "OOP106", "result": "pass"})

        if args.platform == "macos":
            oop106 = driver.find_element(
                AppiumBy.XPATH,
                "//XCUIElementTypeStaticText[@value='OOP106']/ancestor::XCUIElementTypeCell[1]",
            )
        else:
            oop106 = driver.find_element(AppiumBy.NAME, "OOP106")
        oop106.click()
        wait_until(driver, lambda: "OOP106" in text_of(driver, "DetailRule"), timeout=20)
        actions.append({"action": "select-detail", "rule": "OOP106", "result": "pass"})

        language = find(driver, "LanguageSelector")
        language.click()
        language.send_keys("English")
        language.send_keys(Keys.ENTER)
        time.sleep(1)
        actions.append({"action": "language", "value": "en", "result": "pass"})

        driver.set_window_size(900, 600)
        time.sleep(1)
        screenshot(driver, evidence / "resized.png")
        actions.append({"action": "resize", "value": "900x600", "result": "pass"})

        state = {
            "platform": args.platform,
            "window": driver.get_window_size(),
            "target": str(fixture),
            "summary": {
                "danger": text_of(driver, "DangerCount"),
                "warning": text_of(driver, "WarningCount"),
                "attention": text_of(driver, "AttentionCount"),
            },
            "selectedRule": text_of(driver, "DetailRule"),
            "status": text_of(driver, "Status"),
            "search": "OOP106",
            "language": "en",
        }
        (evidence / "ui-state.json").write_text(
            json.dumps(state, indent=2, ensure_ascii=False), encoding="utf-8"
        )
        return 0
    except Exception as exc:
        actions.append({
            "action": "failure",
            "result": "fail",
            "errorType": type(exc).__name__,
            "error": str(exc),
        })
        if driver is not None:
            try:
                screenshot(driver, evidence / "failure.png")
                (evidence / "page-source.xml").write_text(driver.page_source, encoding="utf-8")
            except Exception:
                pass
        print(f"Appium UI E2E failed: {exc}", file=sys.stderr)
        return 1
    finally:
        (evidence / "action-log.json").write_text(
            json.dumps(actions, indent=2, ensure_ascii=False), encoding="utf-8"
        )
        if driver is not None:
            try:
                driver.quit()
            except Exception:
                pass


if __name__ == "__main__":
    raise SystemExit(main())
