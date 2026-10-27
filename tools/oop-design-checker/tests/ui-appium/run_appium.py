#!/usr/bin/env python3
import argparse
import ctypes
import inspect
import json
import os
import sys
import time
import traceback
import xml.etree.ElementTree as ET
from pathlib import Path

from appium import webdriver
from appium.webdriver.common.appiumby import AppiumBy
from selenium.common.exceptions import (
    NoSuchElementException,
    StaleElementReferenceException,
    TimeoutException,
)
from selenium.webdriver.common.keys import Keys
from selenium.webdriver.remote.command import Command
from selenium.webdriver.support.ui import WebDriverWait

from appium_diagnostics import (
    失敗報告を組み立てる,
    待機状況を組み立てる,
    証跡を取得する,
    失敗報告を書式化する,
    失敗報告を書き出す,
)
from macos_bundle import アプリバンドルを検証する


_ACTIVE_FAILURE_CONTEXT = {}
_LAST_AUTOMATION_ID = None


def 引数を解析する():
    parser = argparse.ArgumentParser()
    parser.add_argument("--platform", choices=["windows", "macos"], required=True)
    parser.add_argument("--app", required=True)
    parser.add_argument("--fixture", required=True)
    parser.add_argument("--evidence", required=True)
    parser.add_argument("--server", default="http://127.0.0.1:4723")
    return parser.parse_args()


def 期待値を読み込む(fixture: Path):
    manifest = json.loads((fixture / "expected-diagnostics.json").read_text(encoding="utf-8"))
    diagnostics = manifest["exactDiagnostics"]
    counts = {
        "danger": sum(item["severity"] == "danger" for item in diagnostics),
        "warning": sum(item["severity"] == "warning" for item in diagnostics),
        "attention": sum(item["severity"] == "attention" for item in diagnostics),
    }
    return diagnostics, counts


def Appiumドライバーを作る(args):
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

    bundle_id = アプリバンドルを検証する(Path(app))
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


def Windows接続済みドライバーを作る(args, window_handle):
    from appium.options.windows import WindowsOptions

    options = WindowsOptions()
    options.platform_name = "Windows"
    options.automation_name = "NovaWindows"
    options.app_top_level_window = hex(window_handle)
    options.set_capability("appium:shouldCloseApp", False)
    options.set_capability("appium:newCommandTimeout", 180)
    return webdriver.Remote(args.server, options=options)


def Windows最上位ウィンドウを探す(title, timeout=20):
    if os.name != "nt":
        raise RuntimeError("Windows top-level window discovery requires Windows.")

    from ctypes import wintypes

    user32 = ctypes.windll.user32
    enum_proc_type = ctypes.WINFUNCTYPE(
        wintypes.BOOL,
        wintypes.HWND,
        wintypes.LPARAM,
    )
    deadline = time.monotonic() + timeout

    while time.monotonic() < deadline:
        matches = []

        def ウィンドウを列挙する(hwnd, _):
            if not user32.IsWindowVisible(hwnd):
                return True

            length = user32.GetWindowTextLengthW(hwnd)
            if length <= 0:
                return True

            buffer = ctypes.create_unicode_buffer(length + 1)
            user32.GetWindowTextW(hwnd, buffer, length + 1)
            if buffer.value == title:
                matches.append(int(hwnd))
            return True

        callback = enum_proc_type(ウィンドウを列挙する)
        user32.EnumWindows(callback, 0)
        if matches:
            return matches[-1]

        time.sleep(0.25)

    raise TimeoutError(f"Could not find Windows top-level window: {title}")


def 要素を探す(driver, automation_id):
    global _LAST_AUTOMATION_ID
    _LAST_AUTOMATION_ID = automation_id
    _ACTIVE_FAILURE_CONTEXT["automationId"] = automation_id
    _ACTIVE_FAILURE_CONTEXT["action"] = f"find element: {automation_id}"
    return driver.find_element(AppiumBy.ACCESSIBILITY_ID, automation_id)


def 要素が表示される(driver, automation_id):
    try:
        return 要素を探す(driver, automation_id).is_displayed()
    except (NoSuchElementException, StaleElementReferenceException):
        return False


def 条件成立まで待つ(driver, predicate, timeout=90, expected_condition=None, automation_id=None):
    caller = inspect.currentframe().f_back
    try:
        predicate_source = inspect.getsource(predicate).strip()
    except (OSError, TypeError):
        predicate_source = getattr(predicate, "__name__", "predicate")
    condition = expected_condition or predicate_source
    context = 待機状況を組み立てる(
        _ACTIVE_FAILURE_CONTEXT,
        condition,
        automation_id or _LAST_AUTOMATION_ID,
        timeout,
        f"{Path(caller.f_code.co_filename).name}:{caller.f_lineno}",
    )
    try:
        WebDriverWait(driver, timeout, poll_frequency=0.5).until(lambda _: predicate())
    except TimeoutException as exc:
        context["automationId"] = automation_id or _LAST_AUTOMATION_ID
        exc.appium_context = context
        raise


def 要素の文字列を取得する(driver, automation_id):
    element = 要素を探す(driver, automation_id)
    getters = (
        lambda: element.text,
        lambda: element.get_attribute("Name"),
        lambda: element.get_attribute("Value.Value"),
    )
    for getter in getters:
        try:
            value = getter()
        except Exception:
            continue
        if value:
            return value
    return ""


def コントロールが値を含む(driver, automation_id, expected_values):
    expected = tuple(value.lower() for value in expected_values)
    element = 要素を探す(driver, automation_id)
    getters = (
        lambda: element.text,
        lambda: element.get_attribute("Name"),
        lambda: element.get_attribute("Value.Value"),
        lambda: element.get_attribute("value"),
        lambda: element.get_attribute("title"),
        lambda: element.get_attribute("label"),
    )
    for getter in getters:
        try:
            value = getter()
        except Exception:
            continue
        if value and any(expected_value in str(value).lower() for expected_value in expected):
            return True

    source = driver.page_source
    markers = (
        f'AutomationId="{automation_id}"',
        f'identifier="{automation_id}"',
    )
    for marker in markers:
        index = source.find(marker)
        if index < 0:
            continue
        segment = source[index : index + 3000].lower()
        if any(value in segment for value in expected):
            return True
    return False


def コントロールがキーボードフォーカスを持つ(driver, automation_id):
    global _LAST_AUTOMATION_ID
    _LAST_AUTOMATION_ID = automation_id
    try:
        root = ET.fromstring(driver.page_source)
    except ET.ParseError:
        return False

    for element in root.iter():
        attributes = {
            "".join(character for character in name.lower() if character.isalnum()): value
            for name, value in element.attrib.items()
        }
        element_id = attributes.get("automationid") or attributes.get("identifier")
        if element_id != automation_id:
            continue
        return any(
            attributes.get(name, "").lower() == "true"
            for name in ("haskeyboardfocus", "iskeyboardfocused", "focused", "isfocused")
        ) or any(
            "keyboardfocus" in name and value.lower() == "true"
            for name, value in attributes.items()
        )
    return False


def 入力欄を書き換える(driver, automation_id, value):
    element = 要素を探す(driver, automation_id)
    # Windowsではキーボード入力の送信先を明示してから値を変更します。
    if driver.capabilities.get("platformName", "").lower() == "windows" and automation_id in (
        "TargetPath",
        "ConfigurationPath",
    ):
        element.click()
    element.clear()
    element.send_keys(value)
    return element


def 入力欄の値を取得する(driver, platform, automation_id):
    # Windowsの編集欄はNameやtextに値を公開しないため、ValuePatternを読み取ります。
    element = 要素を探す(driver, automation_id)
    if platform == "windows":
        return driver.execute_script("windows: getValue", element)
    return element.get_attribute("value")


def 画面を撮影する(driver, path: Path):
    path.parent.mkdir(parents=True, exist_ok=True)
    driver.save_screenshot(str(path))


def キー操作を実行する(driver, values):
    actions = []
    for value in values:
        actions.extend(
            [
                {"type": "keyDown", "value": value},
                {"type": "keyUp", "value": value},
            ]
        )

    driver.execute(
        Command.W3C_ACTIONS,
        {
            "actions": [
                {
                    "type": "key",
                    "id": "keyboard",
                    "actions": actions,
                }
            ]
        },
    )
    driver.execute(Command.W3C_CLEAR_ACTIONS, {})


def Altアクセスキーを押す(driver, key):
    driver.execute(
        Command.W3C_ACTIONS,
        {
            "actions": [
                {
                    "type": "key",
                    "id": "keyboard",
                    "actions": [
                        {"type": "keyDown", "value": Keys.ALT},
                        {"type": "keyDown", "value": key},
                        {"type": "keyUp", "value": key},
                        {"type": "keyUp", "value": Keys.ALT},
                    ],
                }
            ]
        },
    )
    driver.execute(Command.W3C_CLEAR_ACTIONS, {})
    time.sleep(0.4)


def Ctrlアクセスキーを押す(driver, key):
    driver.execute(
        Command.W3C_ACTIONS,
        {
            "actions": [
                {
                    "type": "key",
                    "id": "keyboard",
                    "actions": [
                        {"type": "keyDown", "value": Keys.CONTROL},
                        {"type": "keyDown", "value": key},
                        {"type": "keyUp", "value": key},
                        {"type": "keyUp", "value": Keys.CONTROL},
                    ],
                }
            ]
        },
    )
    driver.execute(Command.W3C_CLEAR_ACTIONS, {})
    time.sleep(0.3)


def メニューアクセスキーを押す(driver, key):
    キー操作を実行する(driver, [key])
    time.sleep(0.3)


def メニューアクセス経路を実行する(driver, top_level, *access_keys):
    Altアクセスキーを押す(driver, top_level)
    for key in access_keys:
        メニューアクセスキーを押す(driver, key)


def 集計が一致する(driver, counts):
    return all(
        str(counts[key]) in 要素の文字列を取得する(driver, control_id)
        for key, control_id in (
            ("danger", "DangerCount"),
            ("warning", "WarningCount"),
            ("attention", "AttentionCount"),
        )
    )


def 編集コントロールを取得する(driver, platform, automation_id, fallback_name):
    try:
        return 要素を探す(driver, automation_id)
    except Exception:
        if platform == "windows":
            return driver.find_element(AppiumBy.NAME, fallback_name)
        raise


def 設定編集画面を取得する(driver, platform):
    try:
        return 要素を探す(driver, "ConfigurationEditor")
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


def 規則を選択する(driver, platform, rule_id):
    入力欄を書き換える(driver, "SearchFilter", rule_id)
    time.sleep(1)
    if platform == "macos":
        element = driver.find_element(
            AppiumBy.XPATH,
            f"//XCUIElementTypeStaticText[@value='{rule_id}']/ancestor::XCUIElementTypeCell[1]",
        )
    else:
        element = driver.find_element(AppiumBy.NAME, rule_id)
    element.click()
    条件成立まで待つ(driver, lambda: rule_id in 要素の文字列を取得する(driver, "DetailRule"), timeout=20)


def シナリオ状態を書き出す(driver, directory: Path, scenario, extra=None):
    directory.mkdir(parents=True, exist_ok=True)
    state = {
        "scenario": scenario,
        "window": driver.get_window_size(),
        "summary": {
            "danger": 要素の文字列を取得する(driver, "DangerCount"),
            "warning": 要素の文字列を取得する(driver, "WarningCount"),
            "attention": 要素の文字列を取得する(driver, "AttentionCount"),
        },
        "status": 要素の文字列を取得する(driver, "Status"),
        "selectedRule": 要素の文字列を取得する(driver, "DetailRule"),
        "selectedFile": 要素の文字列を取得する(driver, "DetailLocation"),
        "language": 要素の文字列を取得する(driver, "LanguageSelector"),
        "theme": 要素の文字列を取得する(driver, "ThemeSelector"),
    }
    if extra:
        state.update(extra)
    (directory / "ui-state.json").write_text(
        json.dumps(state, indent=2, ensure_ascii=False), encoding="utf-8"
    )


def 出力JSONを確認する(path: Path, expected_count):
    document = json.loads(path.read_text(encoding="utf-8-sig"))
    diagnostics = document.get("diagnostics", [])
    if document.get("version") != 1 or len(diagnostics) != expected_count:
        raise AssertionError(
            f"JSON export mismatch: version={document.get('version')} count={len(diagnostics)}"
        )


def 出力文書の準備完了を確認する(path: Path, validator, expected_count):
    if not path.exists() or path.stat().st_size == 0:
        return False
    try:
        validator(path, expected_count)
        return True
    except (AssertionError, json.JSONDecodeError, OSError):
        return False


def 出力SARIFを確認する(path: Path, expected_count):
    document = json.loads(path.read_text(encoding="utf-8-sig"))
    runs = document.get("runs", [])
    results = runs[0].get("results", []) if len(runs) == 1 else []
    if document.get("version") != "2.1.0" or len(results) != expected_count:
        raise AssertionError(
            f"SARIF export mismatch: version={document.get('version')} count={len(results)}"
        )


def main():
    args = 引数を解析する()
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

    diagnostics, expected_counts = 期待値を読み込む(fixture)
    actions = []
    scenario_actions = {}

    def 記録する(scenario, action, result="pass", **details):
        _ACTIVE_FAILURE_CONTEXT["scenario"] = scenario
        _ACTIVE_FAILURE_CONTEXT["action"] = action
        if result == "pass":
            _ACTIVE_FAILURE_CONTEXT["lastSuccessfulAction"] = f"{scenario}: {action}"
        item = {"scenario": scenario, "action": action, "result": result, **details}
        actions.append(item)
        scenario_actions.setdefault(scenario, []).append(item)

    def 証跡を撮影する(driver, scenario, filename="after.png", extra=None):
        directory = evidence / scenario
        画面を撮影する(driver, directory / filename)
        シナリオ状態を書き出す(driver, directory, scenario, extra)

    for scenario, expected in {
        "01-cancel": {
            "status": "Analysis cancelled.",
            "disabledAccessKeys": ["Alt+F,L", "Alt+V,L,J"],
        },
        "02-fixture-analysis": {"count": len(diagnostics), "summary": expected_counts},
        "03-filter-detail": {"ruleId": "OOP106"},
        "04-language-resize": {"language": "en"},
        "05-config-editor": {"disabledRules": ["OOP105"]},
        "06-export": {"count": len(diagnostics), "formats": ["json", "sarif"]},
        "07-source-open-fallback": {"ruleId": "OOP106"},
        "08-error-state": {
            "diagnostics": 0,
            "fields": ["Target", "Configuration"],
            "correction": "success",
        },
        "09-keyboard-menu": {
            "topMenus": ["File", "Analyze", "View"],
            "analyzeCancel": "menu-access-key",
            "language": ["ja", "en"],
            "theme": ["Dark", "Light"],
        },
    }.items():
        directory = evidence / scenario
        directory.mkdir(parents=True, exist_ok=True)
        (directory / "expected.json").write_text(
            json.dumps(expected, indent=2, ensure_ascii=False), encoding="utf-8"
        )

    driver = None
    desktop_driver = None
    moved_source = None
    try:
        driver = Appiumドライバーを作る(args)
        記録する("02-fixture-analysis", "session-start", platform=args.platform)
        画面を撮影する(driver, evidence / "02-fixture-analysis" / "before.png")
        入力欄を書き換える(driver, "TargetPath", str(fixture))
        記録する("02-fixture-analysis", "set-target", value=str(fixture))

        keyboard = evidence / "09-keyboard-menu"
        shortcut_hints = 要素の文字列を取得する(driver, "ShortcutHints")
        for expected_shortcut in ("F5", "Esc", "Ctrl+F"):
            if expected_shortcut not in shortcut_hints:
                raise AssertionError(
                    f"Visible shortcut hint is missing {expected_shortcut}: {shortcut_hints}"
                )
        if not コントロールが値を含む(driver, "TargetPathLabel", ("Alt+T",)):
            raise AssertionError("Target label does not expose Alt+T.")
        if not コントロールが値を含む(driver, "ConfigurationPathLabel", ("Alt+C",)):
            raise AssertionError("Configuration label does not expose Alt+C.")
        記録する(
            "09-keyboard-menu",
            "visible-shortcut-hints",
            shortcuts=["F5", "Esc", "Ctrl+F", "Alt+T", "Alt+C"],
        )

        Altアクセスキーを押す(driver, "t")
        if args.platform == "macos":
            driver.switch_to.active_element.send_keys("__target_focus__")
            条件成立まで待つ(
                driver,
                lambda: コントロールが値を含む(driver, "TargetPath", ("__target_focus__",)),
                timeout=20,
            )
        else:
            条件成立まで待つ(
                driver,
                lambda: コントロールがキーボードフォーカスを持つ(driver, "TargetPath"),
                timeout=20,
            )
        記録する("09-keyboard-menu", "focus-target", accessKey="Alt+T")
        入力欄を書き換える(driver, "TargetPath", str(fixture))

        入力欄を書き換える(driver, "ConfigurationPath", "")
        Altアクセスキーを押す(driver, "c")
        if args.platform == "macos":
            driver.switch_to.active_element.send_keys("__config_focus__")
            条件成立まで待つ(
                driver,
                lambda: コントロールが値を含む(
                    driver,
                    "ConfigurationPath",
                    ("__config_focus__",),
                ),
                timeout=20,
            )
        else:
            条件成立まで待つ(
                driver,
                lambda: コントロールがキーボードフォーカスを持つ(driver, "ConfigurationPath"),
                timeout=20,
            )
        記録する("09-keyboard-menu", "focus-configuration", accessKey="Alt+C")
        入力欄を書き換える(driver, "ConfigurationPath", "")

        入力欄を書き換える(driver, "SearchFilter", "")
        Ctrlアクセスキーを押す(driver, "f")
        if args.platform == "macos":
            driver.switch_to.active_element.send_keys("shortcut-check")
            条件成立まで待つ(
                driver,
                lambda: コントロールが値を含む(driver, "SearchFilter", ("shortcut-check",)),
                timeout=20,
            )
        else:
            条件成立まで待つ(
                driver,
                lambda: コントロールがキーボードフォーカスを持つ(driver, "SearchFilter"),
                timeout=20,
            )
        記録する("09-keyboard-menu", "focus-search", shortcut="Ctrl+F")
        入力欄を書き換える(driver, "SearchFilter", "")

        メニューアクセスキーを押す(driver, Keys.F5)
        条件成立まで待つ(driver, lambda: 要素を探す(driver, "CancelButton").is_enabled(), timeout=20)
        記録する("09-keyboard-menu", "analyze-direct-shortcut", shortcut="F5")
        要素を探す(driver, "CancelButton").click()
        条件成立まで待つ(driver, lambda: 要素を探す(driver, "AnalyzeButton").is_enabled(), timeout=20)

        画面を撮影する(driver, keyboard / "before.png")
        Altアクセスキーを押す(driver, "f")
        条件成立まで待つ(driver, lambda: 要素を探す(driver, "TargetFileMenuItem").is_displayed(), timeout=20)
        画面を撮影する(driver, keyboard / "menu-open.png")
        記録する("09-keyboard-menu", "open-file-menu", accessKey="Alt+F")
        メニューアクセスキーを押す(driver, Keys.ESCAPE)

        Altアクセスキーを押す(driver, "v")
        条件成立まで待つ(driver, lambda: 要素を探す(driver, "ThemeMenu").is_displayed(), timeout=20)
        メニューアクセスキーを押す(driver, "t")
        メニューアクセスキーを押す(driver, "l")
        条件成立まで待つ(
            driver,
            lambda: コントロールが値を含む(driver, "ThemeSelector", ("light", "ライト")),
            timeout=20,
        )
        記録する("09-keyboard-menu", "theme-light", accessKey="Alt+V,T,L")

        メニューアクセス経路を実行する(driver, "v", "t", "d")
        条件成立まで待つ(
            driver,
            lambda: コントロールが値を含む(driver, "ThemeSelector", ("dark", "ダーク")),
            timeout=20,
        )
        記録する("09-keyboard-menu", "theme-dark", accessKey="Alt+V,T,D")

        メニューアクセス経路を実行する(driver, "v", "l", "e")
        条件成立まで待つ(
            driver,
            lambda: コントロールが値を含む(driver, "LanguageSelector", ("English",)),
            timeout=20,
        )
        記録する("09-keyboard-menu", "language-english", accessKey="Alt+V,L,E")

        証跡を撮影する(
            driver,
            "09-keyboard-menu",
            extra={"accessKeys": ["Alt+F", "Alt+A", "Alt+V", "Alt+T", "Alt+C"]},
        )

        # 解析中に無効な操作をキーから実行しても、設定と言語が変わらないことを確認します。
        入力欄を書き換える(driver, "ConfigurationPath", "busy-menu-check.json")
        操作前の設定パス = 入力欄の値を取得する(driver, args.platform, "ConfigurationPath")
        if not 操作前の設定パス:
            raise AssertionError("The disabled-menu test requires a nonempty configuration path.")
        メニューアクセス経路を実行する(driver, "a", "r")
        記録する("01-cancel", "analyze", result="started", accessKey="Alt+A,R")
        条件成立まで待つ(driver, lambda: 要素を探す(driver, "CancelButton").is_enabled(), timeout=20)
        メニューアクセス経路を実行する(driver, "f", "l")
        メニューアクセスキーを押す(driver, Keys.ESCAPE)
        if 入力欄の値を取得する(driver, args.platform, "ConfigurationPath") != 操作前の設定パス:
            raise AssertionError("Disabled configuration clear executed through Alt+F,L.")
        メニューアクセス経路を実行する(driver, "v", "l", "j")
        メニューアクセスキーを押す(driver, Keys.ESCAPE)
        if not コントロールが値を含む(driver, "LanguageSelector", ("English",)):
            raise AssertionError("Disabled language menu changed the language during analysis.")
        if not 要素を探す(driver, "CancelButton").is_enabled():
            raise AssertionError("Analysis ended before the disabled-menu checks completed.")
        記録する("01-cancel", "disabled-menu-access", accessKeys=["Alt+F,L", "Alt+V,L,J"])
        メニューアクセス経路を実行する(driver, "a", "c")
        条件成立まで待つ(
            driver,
            lambda: "cancelled" in 要素の文字列を取得する(driver, "Status").lower()
            or "キャンセル" in 要素の文字列を取得する(driver, "Status"),
            timeout=20,
        )
        記録する("01-cancel", "cancel", accessKey="Alt+A,C")
        証跡を撮影する(driver, "01-cancel")
        条件成立まで待つ(driver, lambda: 要素を探す(driver, "AnalyzeButton").is_enabled(), timeout=20)
        入力欄を書き換える(driver, "ConfigurationPath", "")

        要素を探す(driver, "AnalyzeButton").click()
        記録する("02-fixture-analysis", "analyze", result="started")
        条件成立まで待つ(driver, lambda: 集計が一致する(driver, expected_counts), timeout=120)
        if 入力欄の値を取得する(driver, args.platform, "ConfigurationPath"):
            raise AssertionError("Config-free GUI analysis must keep the configuration field empty.")
        configuration_summary = 要素の文字列を取得する(driver, "ConfigurationSummary")
        if not any(
            marker in configuration_summary.lower()
            for marker in ("default", "既定値")
        ):
            raise AssertionError(
                f"Config-free GUI analysis did not show built-in defaults: {configuration_summary}"
            )
        記録する(
            "02-fixture-analysis",
            "analyze",
            result="pass",
            configuration="built-in-defaults",
        )
        証跡を撮影する(
            driver,
            "02-fixture-analysis",
            extra={
                "target": str(fixture),
                "platform": args.platform,
                "configurationPath": "",
                "configurationSummary": configuration_summary,
            },
        )

        規則を選択する(driver, args.platform, "OOP106")
        記録する("03-filter-detail", "filter-select", ruleId="OOP106")
        証跡を撮影する(driver, "03-filter-detail")

        language = 要素を探す(driver, "LanguageSelector")
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
            条件成立まで待つ(
                driver,
                lambda: 要素を探す(driver, "LanguageSelector").get_attribute("value") == "English",
                timeout=20,
            )
        else:
            language.send_keys("English")
            language.send_keys(Keys.ENTER)
        条件成立まで待つ(
            driver,
            lambda: "Rule:" in 要素の文字列を取得する(driver, "DetailRule"),
            timeout=20,
        )
        記録する("04-language-resize", "language", value="en")

        if args.platform == "macos":
            resize_value = "native-resize-covered-by-headless"
        else:
            driver.set_window_size(900, 600)
            resize_value = "900x600"
        time.sleep(1)
        記録する("04-language-resize", "resize", value=resize_value)
        証跡を撮影する(
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
        入力欄を書き換える(driver, "ConfigurationPath", str(config_path))
        main_window = driver.current_window_handle if args.platform == "windows" else None
        desktop_driver = None
        editor_driver = driver
        edit_button = 要素を探す(driver, "EditConfigurationButton")
        edit_button.click()

        if args.platform == "windows":
            time.sleep(0.75)
            try:
                編集コントロールを取得する(
                    driver,
                    args.platform,
                    "ValidateConfigurationButton",
                    "Validate",
                )
            except Exception:
                edit_button.send_keys(Keys.ENTER)
                time.sleep(0.75)
                try:
                    編集コントロールを取得する(
                        driver,
                        args.platform,
                        "ValidateConfigurationButton",
                        "Validate",
                    )
                except Exception:
                    edit_button.send_keys(Keys.SPACE)
                    time.sleep(0.75)

        def 設定編集画面へ切り替える():
            if args.platform == "windows":
                handles = list(driver.window_handles)
                handles.sort(key=lambda handle: handle == main_window)
                for handle in handles:
                    try:
                        driver.switch_to.window(handle)
                        編集コントロールを取得する(
                            driver,
                            args.platform,
                            "ValidateConfigurationButton",
                            "Validate",
                        )
                        設定編集画面を取得する(driver, args.platform)
                        return True
                    except Exception:
                        continue

                if main_window is not None:
                    driver.switch_to.window(main_window)
                return False

            try:
                設定編集画面を取得する(driver, args.platform)
                return True
            except Exception:
                return False

        if args.platform == "windows":
            try:
                条件成立まで待つ(driver, 設定編集画面へ切り替える, timeout=5)
            except Exception:
                native_window_handle = Windows最上位ウィンドウを探す(
                    "OOP Design Checker configuration",
                    timeout=20,
                )
                desktop_driver = Windows接続済みドライバーを作る(
                    args,
                    native_window_handle,
                )
                editor_driver = desktop_driver
                条件成立まで待つ(
                    editor_driver,
                    lambda: bool(
                        編集コントロールを取得する(
                            editor_driver,
                            args.platform,
                            "ValidateConfigurationButton",
                            "Validate",
                        )
                    ),
                    timeout=20,
                )
        else:
            条件成立まで待つ(driver, 設定編集画面へ切り替える, timeout=20)

        _ = 設定編集画面を取得する(editor_driver, args.platform)
        編集コントロールを取得する(
            editor_driver,
            args.platform,
            "ValidateConfigurationButton",
            "Validate",
        ).click()

        def 設定が有効である():
            try:
                return "valid" in 要素の文字列を取得する(
                    editor_driver,
                    "ConfigurationEditorStatus",
                ).lower()
            except Exception:
                if args.platform == "windows":
                    try:
                        editor_driver.find_element(
                            AppiumBy.NAME,
                            "Configuration is valid.",
                        )
                        return True
                    except Exception:
                        return False
                return False

        条件成立まで待つ(editor_driver, 設定が有効である, timeout=20)
        編集コントロールを取得する(
            editor_driver,
            args.platform,
            "SaveConfigurationButton",
            "Save",
        ).click()

        if desktop_driver is not None:
            desktop_driver.quit()
            desktop_driver = None

        if args.platform == "windows":
            条件成立まで待つ(
                driver,
                lambda: main_window in driver.window_handles,
                timeout=20,
            )
            driver.switch_to.window(main_window)

        条件成立まで待つ(driver, lambda: 要素を探す(driver, "AnalyzeButton").is_enabled(), timeout=20)
        if "OOP105" not in config_path.read_text(encoding="utf-8"):
            raise AssertionError("Configuration editor save lost the configured disabled rule.")
        記録する(
            "05-config-editor",
            "edit-validate-save",
            path=str(config_path),
            status=要素の文字列を取得する(driver, "Status"),
        )

        要素を探す(driver, "AnalyzeButton").click()
        configured_counts = dict(expected_counts)
        configured_counts["warning"] -= 1
        条件成立まで待つ(driver, lambda: 集計が一致する(driver, configured_counts), timeout=120)
        記録する("05-config-editor", "reanalyze", disabledRule="OOP105")
        証跡を撮影する(
            driver,
            "05-config-editor",
            extra={"configurationPath": str(config_path), "summary": configured_counts},
        )

        要素を探す(driver, "ClearConfigurationButton").click()
        条件成立まで待つ(driver, lambda: 要素を探す(driver, "AnalyzeButton").is_enabled(), timeout=20)
        メニューアクセスキーを押す(driver, Keys.F5)
        条件成立まで待つ(driver, lambda: 集計が一致する(driver, expected_counts), timeout=120)
        記録する("06-export", "reanalyze-defaults")

        json_path = export_dir / "actual-diagnostics.json"
        sarif_path = export_dir / "actual-diagnostics.sarif"
        for path in (json_path, sarif_path):
            path.unlink(missing_ok=True)

        メニューアクセス経路を実行する(driver, "f", "j")
        条件成立まで待つ(
            driver,
            lambda: 出力文書の準備完了を確認する(
                json_path, 出力JSONを確認する, len(diagnostics)
            ),
            timeout=20,
        )
        記録する("06-export", "export-json", path=str(json_path))

        メニューアクセス経路を実行する(driver, "f", "s")
        条件成立まで待つ(
            driver,
            lambda: 出力文書の準備完了を確認する(
                sarif_path, 出力SARIFを確認する, len(diagnostics)
            ),
            timeout=20,
        )
        記録する("06-export", "export-sarif", path=str(sarif_path))

        export_scenario = evidence / "06-export"
        (export_scenario / "actual-diagnostics.json").write_bytes(json_path.read_bytes())
        (export_scenario / "actual-diagnostics.sarif").write_bytes(sarif_path.read_bytes())
        証跡を撮影する(driver, "06-export")

        規則を選択する(driver, args.platform, "OOP106")
        location_text = 要素の文字列を取得する(driver, "DetailLocation")
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
            メニューアクセス経路を実行する(driver, "a", "o")
            条件成立まで待つ(
                driver,
                lambda: "not found" in 要素の文字列を取得する(driver, "Status").lower(),
                timeout=20,
            )
            記録する("07-source-open-fallback", "open-missing-source", file=str(source_path))
            証跡を撮影する(driver, "07-source-open-fallback")
        finally:
            if backup_path.exists():
                backup_path.rename(source_path)
            moved_source = None

        error_evidence = evidence / "08-error-state"
        missing_target = fixture / "__missing-target__.cs"
        if missing_target.exists():
            raise AssertionError(f"Missing-target fixture unexpectedly exists: {missing_target}")
        入力欄を書き換える(driver, "ConfigurationPath", "")
        入力欄を書き換える(driver, "TargetPath", str(missing_target))
        メニューアクセスキーを押す(driver, Keys.F5)
        条件成立まで待つ(
            driver,
            lambda: 要素が表示される(driver, "TargetError"),
            timeout=120,
            expected_condition="TargetError is visible after a missing target",
            automation_id="TargetError",
        )
        if 要素が表示される(driver, "ConfigurationError"):
            raise AssertionError("Missing target incorrectly marked the configuration field.")
        target_error = 要素の文字列を取得する(driver, "TargetError")
        if not target_error.strip():
            raise AssertionError("Target field did not expose an inline error message.")
        target_status = 要素の文字列を取得する(driver, "Status")
        if "target" not in target_status.lower():
            raise AssertionError(
                f"Status did not direct the user to the Target field: {target_status}"
            )
        記録する(
            "08-error-state",
            "missing-target",
            status=要素の文字列を取得する(driver, "Status"),
            field="Target",
        )
        証跡を撮影する(
            driver,
            "08-error-state",
            "missing-target.png",
            {"target": str(missing_target), "field": "Target"},
        )

        missing_config = error_evidence / "missing-config.json"
        入力欄を書き換える(driver, "TargetPath", str(fixture))
        入力欄を書き換える(driver, "ConfigurationPath", str(missing_config))
        メニューアクセスキーを押す(driver, Keys.F5)
        条件成立まで待つ(
            driver,
            lambda: 要素が表示される(driver, "ConfigurationError"),
            timeout=120,
            expected_condition="ConfigurationError is visible after a missing explicit config",
            automation_id="ConfigurationError",
        )
        if 要素が表示される(driver, "TargetError"):
            raise AssertionError("Missing config incorrectly marked the target field.")
        config_error = 要素の文字列を取得する(driver, "ConfigurationError")
        if "configuration" not in config_error.lower():
            raise AssertionError(
                f"Configuration error did not explain the missing config: {config_error}"
            )
        記録する(
            "08-error-state",
            "missing-configuration",
            status=要素の文字列を取得する(driver, "Status"),
            field="Configuration",
        )
        証跡を撮影する(
            driver,
            "08-error-state",
            "missing-configuration.png",
            {"configurationPath": str(missing_config), "field": "Configuration"},
        )

        invalid_config = error_evidence / "invalid-config.json"
        invalid_config.write_text("{ not-json", encoding="utf-8")
        入力欄を書き換える(driver, "ConfigurationPath", str(invalid_config))
        メニューアクセスキーを押す(driver, Keys.F5)
        条件成立まで待つ(
            driver,
            lambda: 要素が表示される(driver, "ConfigurationError"),
            timeout=120,
            expected_condition="ConfigurationError is visible after invalid JSON",
            automation_id="ConfigurationError",
        )
        invalid_config_error = 要素の文字列を取得する(driver, "ConfigurationError")
        if "configuration" not in invalid_config_error.lower():
            raise AssertionError(
                f"Invalid config did not remain classified as configuration: {invalid_config_error}"
            )
        記録する(
            "08-error-state",
            "invalid-configuration",
            status=要素の文字列を取得する(driver, "Status"),
            field="Configuration",
        )
        証跡を撮影する(
            driver,
            "08-error-state",
            extra={"configurationPath": str(invalid_config), "field": "Configuration"},
        )

        入力欄を書き換える(driver, "ConfigurationPath", "")
        要素を探す(driver, "AnalyzeButton").click()
        条件成立まで待つ(driver, lambda: 集計が一致する(driver, expected_counts), timeout=120)
        if 要素が表示される(driver, "TargetError") or 要素が表示される(
            driver, "ConfigurationError"
        ):
            raise AssertionError("Corrected inputs left an inline error visible.")
        記録する("08-error-state", "correct-input-and-reanalyze", result="pass")
        証跡を撮影する(
            driver,
            "08-error-state",
            "corrected.png",
            {"target": str(fixture), "configurationPath": ""},
        )

        return 0
    except Exception as exc:
        traceback_text = traceback.format_exc()
        context = dict(_ACTIVE_FAILURE_CONTEXT)
        wait_context = getattr(exc, "appium_context", {})
        context.update(wait_context)
        traceback_frames = traceback.extract_tb(exc.__traceback__)
        if traceback_frames:
            failure_frame = traceback_frames[-1]
            context["failureLocation"] = (
                f"{Path(failure_frame.filename).name}:{failure_frame.lineno}"
            )
            if not wait_context:
                context["action"] = (
                    f"{failure_frame.name} at {context['failureLocation']}"
                )
                context["source"] = context["failureLocation"]
        記録する(
            context.get("scenario") or "failure",
            context.get("action") or "exception",
            result="fail",
            errorType=type(exc).__name__,
            error=str(exc),
            expectedCondition=context.get("expectedCondition"),
            automationId=context.get("automationId"),
            timeoutSeconds=context.get("timeoutSeconds"),
            source=context.get("source"),
        )
        evidence_paths = {
            "screenshot": str(evidence / "failure.png"),
            "pageSource": str(evidence / "page-source.xml"),
            "actionLog": str(evidence / "action-log.json"),
            "traceback": str(evidence / "traceback.txt"),
            "failureReport": str(evidence / "failure.json"),
        }
        evidence_errors = []

        def 失敗証跡を取得する(name, callback):
            error = 証跡を取得する(name, callback)
            if error:
                evidence_errors.append(error)

        if driver is not None:
            失敗証跡を取得する(
                "screenshot",
                lambda: 画面を撮影する(driver, evidence / "failure.png"),
            )
            失敗証跡を取得する(
                "page source",
                lambda: (evidence / "page-source.xml").write_text(
                    driver.page_source, encoding="utf-8"
                ),
            )
        失敗証跡を取得する(
            "traceback",
            lambda: (evidence / "traceback.txt").write_text(
                traceback_text, encoding="utf-8"
            ),
        )
        report = 失敗報告を組み立てる(
            exc,
            traceback_text,
            context=context,
            evidence_paths=evidence_paths,
            evidence_errors=evidence_errors,
        )
        失敗証跡を取得する(
            "failure report",
            lambda: 失敗報告を書き出す(evidence / "failure.json", report),
        )
        report["evidenceErrors"] = evidence_errors
        print(失敗報告を書式化する(report), file=sys.stderr)
        return 1
    finally:
        if moved_source is not None:
            source_path, backup_path = moved_source
            if backup_path.exists():
                backup_path.rename(source_path)

        action_log_error = 証跡を取得する(
            "action log",
            lambda: (evidence / "action-log.json").write_text(
                json.dumps(actions, indent=2, ensure_ascii=False), encoding="utf-8"
            ),
        )
        if action_log_error:
            print(action_log_error, file=sys.stderr)
        for scenario, items in scenario_actions.items():
            directory = evidence / scenario
            scenario_log_error = 証跡を取得する(
                f"{scenario} action log",
                lambda directory=directory, items=items: (
                    directory.mkdir(parents=True, exist_ok=True),
                    (directory / "action-log.json").write_text(
                        json.dumps(items, indent=2, ensure_ascii=False), encoding="utf-8"
                    ),
                ),
            )
            if scenario_log_error:
                print(scenario_log_error, file=sys.stderr)

        if desktop_driver is not None:
            try:
                desktop_driver.quit()
            except Exception:
                pass

        if driver is not None:
            try:
                driver.quit()
            except Exception:
                pass


if __name__ == "__main__":
    raise SystemExit(main())
