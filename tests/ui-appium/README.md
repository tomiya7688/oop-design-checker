# GUI Appium release E2E

This harness drives the real packaged Avalonia GUI through Appium on Windows and macOS.

The release gate downloads the GUI archive produced by the same workflow, extracts it, and runs these fixed scenarios:

- `fixture-analysis` — target input, analysis, release-fixture summary
- `filter-detail-language-resize` — OOP106 filter/detail, English switch, native resize checkpoint
- `configuration` — configuration editor validate/save and disabled-rule reanalysis
- `export` — JSON and SARIF export through the GUI buttons
- `source-open-fallback` — missing-source fallback after a real diagnostic selection
- `cancel` — cancellation of a multi-project analysis
- `invalid-target-error` — visible error state and cleared diagnostic summary

Each scenario writes its evidence below the configured evidence root:

```text
<scenario-id>/
  before.png
  after.png
  resized.png            # resize scenario only
  ui-state.json
  expected.json
  action-log.json
  actual-diagnostics.json  # export scenario
  actual-diagnostics.sarif # export scenario
```

On failure, `failure/failure.png` and `failure/page-source.xml` are captured when possible.

## Deterministic export destination

Normal GUI usage continues to open the native save picker. CI sets
`OOP_DESIGN_CHECKER_UI_E2E_EXPORT_DIR` so the same export button handlers write into a deterministic temporary directory. The override is inactive unless that environment variable is explicitly set.

The small/default/large pixel/layout contract remains the responsibility of the existing Avalonia.Headless + Skia gate. macOS Mac2 does not expose arbitrary window-size mutation, so its real-window resize checkpoint uses maximize while Windows uses 900x600.
