# GUI Headless Visual Regression

This test project renders the production Avalonia GUI with the headless platform and the Skia renderer.

It validates:

- release fixture diagnostics against the GUI row count and severity summary
- Japanese and English UI states
- small (900x600), default (1280x820), and large (1600x1000) layouts
- filter and selected-diagnostic detail state
- error state
- configuration editor state
- key control bounds and overlap
- committed PNG baselines with a 0.5% changed-pixel threshold

Normal runs never update baselines. Actual frames, visual diffs, and `ui-state.json` are written to `UI_EVIDENCE_DIR`.

To intentionally regenerate baselines on Linux/Skia:

```bash
UPDATE_VISUAL_BASELINES=1 \
UI_EVIDENCE_DIR=/tmp/oop-ui-evidence \
dotnet run --project tests/OopDesignChecker.Gui.HeadlessTests/OopDesignChecker.Gui.HeadlessTests.csproj -c Release
```

Review every changed PNG before committing it. Baseline updates are product UI changes, not an automatic fix for a failed regression.
