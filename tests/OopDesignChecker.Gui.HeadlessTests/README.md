# GUI Headless Tests

This project renders the production Avalonia GUI with the headless platform and the Skia renderer.

The test suite now has two execution modes so ordinary developer test runs are not coupled to a Linux-generated golden image.

## Structural UI checks

Structural checks are the default on every operating system. They validate:

- release fixture diagnostics against the GUI row count and severity summary
- Japanese and English UI states
- small (900x600), default (1280x820), and large (1600x1000) layouts
- filter and selected-diagnostic detail state
- error state
- configuration editor state
- key control bounds and overlap

Run them with:

```bash
dotnet run --project tests/OopDesignChecker.Gui.HeadlessTests/OopDesignChecker.Gui.HeadlessTests.csproj -c Release
```

No pixel baseline comparison is performed unless the canonical visual-regression mode is explicitly enabled. This keeps Windows and macOS developer test execution, including Visual Studio environments that run tests after build, from failing only because their font/rendering output differs from the committed Linux/Skia baselines.

## Canonical visual regression

PNG golden-image comparison is pinned to Linux + Skia. Enable it explicitly with:

```bash
OOP_DESIGN_CHECKER_UI_VISUAL_REGRESSION=1 \
UI_EVIDENCE_DIR=/tmp/oop-ui-evidence \
dotnet run --project tests/OopDesignChecker.Gui.HeadlessTests/OopDesignChecker.Gui.HeadlessTests.csproj -c Release
```

This mode:

- captures the rendered frame for each scenario
- writes `actual.png`, `diff.png`, and `ui-state.json` to `UI_EVIDENCE_DIR`
- compares against the committed PNG baselines
- fails when more than 0.5% of pixels differ beyond the per-channel threshold

If canonical visual mode is explicitly requested on Windows or macOS, the test fails with a clear configuration error instead of treating that platform as compatible with the Linux golden images.

The GitHub Actions `ui-headless` job is the canonical visual-regression environment. Normal matrix CI executes structural checks on Windows, Linux, and macOS separately.

## Updating visual baselines

To intentionally regenerate baselines on Linux/Skia:

```bash
UPDATE_VISUAL_BASELINES=1 \
UI_EVIDENCE_DIR=/tmp/oop-ui-evidence \
dotnet run --project tests/OopDesignChecker.Gui.HeadlessTests/OopDesignChecker.Gui.HeadlessTests.csproj -c Release
```

`UPDATE_VISUAL_BASELINES=1` also requires Linux. Review every changed PNG before committing it. Baseline updates are product UI changes, not an automatic fix for a failed regression.
