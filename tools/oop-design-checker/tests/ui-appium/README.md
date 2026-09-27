# Native Appium UI E2E

This harness drives the production Avalonia GUI through Appium on Windows and macOS.

## Platforms

- Windows x64: Appium + NovaWindows
- macOS arm64: Appium + Mac2

The workflow first creates the same archive layout used by the GUI release package, extracts it, and launches the extracted application. macOS wraps the extracted publish directory in a minimal `.app` bundle because Mac2 requires an application bundle.

## Fixed scenarios

Evidence is grouped under stable scenario IDs:

- `01-cancel` - start analysis and cancel it
- `02-fixture-analysis` - analyze the release-validation fixture and verify severity counts
- `03-filter-detail` - filter/select OOP106 and verify detail state
- `04-language-resize` - switch to English and resize/maximize
- `05-config-editor` - edit, validate, save, and apply a configuration
- `06-export` - export JSON and SARIF and validate their contents
- `07-source-open-fallback` - exercise the missing-source fallback after selection
- `08-error-state` - analyze an invalid target and verify the visible error state

Each scenario contains `expected.json`, screenshots where relevant, `ui-state.json`, and `action-log.json`. Export evidence also contains the actual JSON and SARIF diagnostics.

## Deterministic automation hooks

The production GUI behaves normally unless these environment variables are explicitly set by the native UI workflow:

- `OOP_DESIGN_CHECKER_UI_AUTOMATION_ANALYSIS_DELAY_MS` - bounded delay before analysis, used to make cancellation deterministic
- `OOP_DESIGN_CHECKER_UI_AUTOMATION_EXPORT_DIR` - fixed export destination used instead of a native save picker

Normal runs do not set either variable.
