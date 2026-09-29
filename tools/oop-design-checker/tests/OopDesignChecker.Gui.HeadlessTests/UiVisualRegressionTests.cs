using System.Collections;
using System.Text.Json;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media.Imaging;
using Avalonia.Styling;
using Avalonia.Threading;
using OopDesignChecker.Core;
using OopDesignChecker.Localization;
using SkiaSharp;
using Xunit;

namespace OopDesignChecker.Gui.HeadlessTests;

public sealed class UiVisualRegressionTests
{
    private const string VisualRegressionEnvironmentVariable =
        "OOP_DESIGN_CHECKER_UI_VISUAL_REGRESSION";
    private const double MaximumChangedPixelRatio = 0.005;
    private const byte PixelDeltaThreshold = 12;
    private static readonly JsonSerializerOptions IndentedJson = new() { WriteIndented = true };

    [Avalonia.Headless.XUnit.AvaloniaFact]
    public void ReadyStateRendersJapaneseAtSupportedSizes()
    {
        CaptureReadyState("ready-ja-small", 900, 600);
        CaptureReadyState("ready-ja-default", 1280, 820);
        CaptureReadyState("ready-ja-large", 1600, 1000);
    }

    [Avalonia.Headless.XUnit.AvaloniaFact]
    public void EnglishStateRendersWithoutClipping()
    {
        var window = CreateWindow(1280, 820);
        try
        {
            window.SetLanguageForTesting(UserInterfaceLanguage.English);
            Flush();
            AssertMainWindowLayout(window);
            Capture(window, "ready-en-default", "en");
        }
        finally
        {
            window.Close();
        }
    }

    [Avalonia.Headless.XUnit.AvaloniaFact]
    public void ThemeSwitchPreservesAnalysisAndFilterState()
    {
        var fixtureRoot = FixtureRoot();
        var result = CheckerService.Analyze(fixtureRoot);
        var window = CreateWindow(1280, 820);

        try
        {
            Assert.Equal(ThemeVariant.Dark, Avalonia.Application.Current!.RequestedThemeVariant);

            window.SetThemeForTesting(ThemeVariant.Light);
            Flush();
            Assert.Equal(ThemeVariant.Light, Avalonia.Application.Current!.RequestedThemeVariant);
            Assert.Equal(ThemeVariant.Light, window.ActualThemeVariant);
            Assert.Equal(1, window.TestView.ThemeSelector.SelectedIndex);
            Capture(window, "ready-light-default", "ja");

            window.PresentResultForTesting(result);
            window.TestView.DangerFilter.IsChecked = false;
            window.TestView.WarningFilter.IsChecked = true;
            window.TestView.AttentionFilter.IsChecked = false;
            window.TestView.SearchFilter.Text = "OOP106";
            window.ApplyFiltersForTesting();
            Flush();

            var rows = VisibleRows(window);
            Assert.NotEmpty(rows);
            Assert.All(rows, row => Assert.Equal("OOP106", row.RuleId));
            Assert.Equal("OOP106", window.TestView.SearchFilter.Text);
            AssertSummaryMatches(window, result.Diagnostics);
            Capture(window, "fixture-light-default", "ja");

            window.SetThemeForTesting(ThemeVariant.Dark);
            Flush();
            Assert.Equal(ThemeVariant.Dark, Avalonia.Application.Current!.RequestedThemeVariant);
            Assert.Equal("OOP106", window.TestView.SearchFilter.Text);
            Assert.NotEmpty(VisibleRows(window));
        }
        finally
        {
            window.SetThemeForTesting(ThemeVariant.Dark);
            window.Close();
        }
    }

    [Avalonia.Headless.XUnit.AvaloniaFact]
    public void AltAccessKeysOpenMenusInBothLanguages()
    {
        var window = CreateWindow(1280, 820);
        try
        {
            window.TestView.TargetPath.Focus();
            PressKey(window, Key.F, PhysicalKey.F, RawInputModifiers.Alt, "f");
            Assert.True(window.TestView.MainMenu.FileMenu.IsSubMenuOpen);
            Capture(window, "menu-file-open-ja", "ja");
            PressKey(window, Key.Escape, PhysicalKey.Escape);
            Assert.False(window.TestView.MainMenu.FileMenu.IsSubMenuOpen);

            PressKey(window, Key.A, PhysicalKey.A, RawInputModifiers.Alt, "a");
            Assert.True(window.TestView.MainMenu.AnalyzeMenu.IsSubMenuOpen);
            PressKey(window, Key.Escape, PhysicalKey.Escape);

            window.SetLanguageForTesting(UserInterfaceLanguage.English);
            window.TestView.SearchFilter.Focus();
            PressKey(window, Key.V, PhysicalKey.V, RawInputModifiers.Alt, "v");
            Assert.True(window.TestView.MainMenu.ViewMenu.IsSubMenuOpen);
            PressKey(window, Key.Escape, PhysicalKey.Escape);
        }
        finally
        {
            window.Close();
        }
    }

    [Avalonia.Headless.XUnit.AvaloniaFact]
    public void DirectFocusShortcutsPreserveTextAndOrdinaryTypingDoesNotTriggerActions()
    {
        var window = CreateWindow(1280, 820);
        try
        {
            window.TestView.TargetPath.Text = "target-value";
            window.TestView.ConfigurationPath.Text = "config-value";
            window.TestView.SearchFilter.Text = "search-value";

            window.TestView.TargetPath.Focus();
            window.KeyTextInput("f");
            Flush();
            Assert.Equal("target-valuef", window.TestView.TargetPath.Text);
            Assert.False(window.TestView.MainMenu.FileMenu.IsSubMenuOpen);

            window.TestView.SearchFilter.Focus();
            PressKey(window, Key.T, PhysicalKey.T, RawInputModifiers.Alt, "t");
            Assert.Same(window.TestView.TargetPath, window.FocusManager?.GetFocusedElement());
            Assert.Equal("target-valuef", window.TestView.TargetPath.Text);

            PressKey(window, Key.C, PhysicalKey.C, RawInputModifiers.Alt, "c");
            Assert.Same(
                window.TestView.ConfigurationPath,
                window.FocusManager?.GetFocusedElement()
            );
            Assert.Equal("config-value", window.TestView.ConfigurationPath.Text);

            PressKey(window, Key.F, PhysicalKey.F, RawInputModifiers.Control, "f");
            Assert.Same(window.TestView.SearchFilter, window.FocusManager?.GetFocusedElement());
            Assert.Equal("search-value", window.TestView.SearchFilter.Text);
        }
        finally
        {
            window.Close();
        }
    }

    [Avalonia.Headless.XUnit.AvaloniaFact]
    public void EscapeLeavesEditingBeforeCancellingAnalysis()
    {
        const string delayVariable = "OOP_DESIGN_CHECKER_UI_AUTOMATION_ANALYSIS_DELAY_MS";
        var previousDelay = Environment.GetEnvironmentVariable(delayVariable);
        Environment.SetEnvironmentVariable(delayVariable, "30000");

        var window = CreateWindow(1280, 820);
        try
        {
            window.TestView.SearchFilter.Text = "OOP106";
            window.TestView.SearchFilter.Focus();
            window.TestView.AnalyzeButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Flush();

            Assert.True(window.TestView.CancelButton.IsEnabled);
            Assert.Same(window.TestView.SearchFilter, window.FocusManager?.GetFocusedElement());

            PressKey(window, Key.Escape, PhysicalKey.Escape);
            Assert.Equal("OOP106", window.TestView.SearchFilter.Text);
            Assert.NotSame(window.TestView.SearchFilter, window.FocusManager?.GetFocusedElement());
            Assert.True(window.TestView.CancelButton.IsEnabled);

            PressKey(window, Key.Escape, PhysicalKey.Escape);
            Assert.False(window.TestView.CancelButton.IsEnabled);
            WaitUntil(
                () =>
                    window.TestView.Status.Text.Contains(
                        "キャンセル",
                        StringComparison.OrdinalIgnoreCase
                    ),
                timeoutMilliseconds: 3000
            );
        }
        finally
        {
            window.Close();
            Environment.SetEnvironmentVariable(delayVariable, previousDelay);
        }
    }

    [Avalonia.Headless.XUnit.AvaloniaFact]
    public void MenuStateTracksFiltersLanguageThemeAndResultAvailability()
    {
        var fixtureRoot = FixtureRoot();
        var result = CheckerService.Analyze(fixtureRoot);
        var window = CreateWindow(1280, 820);

        try
        {
            var menu = window.TestView.MainMenu;
            Assert.True(menu.AnalyzeItem.IsEnabled);
            Assert.False(menu.CancelItem.IsEnabled);
            Assert.False(menu.ExportJsonItem.IsEnabled);
            Assert.False(menu.OpenSourceItem.IsEnabled);
            Assert.True(menu.DangerFilterItem.IsChecked);
            Assert.True(menu.WarningFilterItem.IsChecked);
            Assert.True(menu.AttentionFilterItem.IsChecked);

            menu.WarningFilterItem.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
            Flush();
            Assert.False(window.TestView.WarningFilter.IsChecked);
            Assert.False(menu.WarningFilterItem.IsChecked);

            menu.LightThemeItem.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
            Flush();
            Assert.Equal(1, window.TestView.ThemeSelector.SelectedIndex);
            Assert.True(menu.LightThemeItem.IsChecked);
            Assert.Equal(ThemeVariant.Light, Avalonia.Application.Current!.RequestedThemeVariant);

            menu.EnglishLanguageItem.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
            Flush();
            Assert.Equal(1, window.TestView.LanguageSelector.SelectedIndex);
            Assert.True(menu.EnglishLanguageItem.IsChecked);

            window.PresentResultForTesting(result);
            Flush();
            Assert.True(menu.ExportJsonItem.IsEnabled);

            var row = VisibleRows(window).First();
            window.TestView.DiagnosticsGrid.SelectedItem = row;
            window.UpdateSelectedDiagnosticForTesting();
            Flush();
            Assert.True(menu.OpenSourceItem.IsEnabled);
        }
        finally
        {
            window.SetThemeForTesting(ThemeVariant.Dark);
            window.Close();
        }
    }

    [Avalonia.Headless.XUnit.AvaloniaFact]
    public void ReleaseFixtureRendersExpectedRowsFilterAndDetail()
    {
        var fixtureRoot = FixtureRoot();
        var expectedCount = ReadExpectedDiagnosticCount(fixtureRoot);
        var result = CheckerService.Analyze(fixtureRoot);

        Assert.Equal(expectedCount, result.Diagnostics.Count);

        var window = CreateWindow(1280, 820);
        try
        {
            window.PresentResultForTesting(result);
            Flush();

            var rows = VisibleRows(window);
            Assert.Equal(expectedCount, rows.Length);
            AssertSummaryMatches(window, result.Diagnostics);
            AssertMainWindowLayout(window);
            Capture(window, "fixture-ja-default", "ja");

            window.TestView.DangerFilter.IsChecked = false;
            window.TestView.WarningFilter.IsChecked = true;
            window.TestView.AttentionFilter.IsChecked = false;
            window.TestView.SearchFilter.Text = "OOP106";
            window.ApplyFiltersForTesting();
            Flush();

            rows = VisibleRows(window);
            Assert.NotEmpty(rows);
            Assert.All(rows, row => Assert.Equal("OOP106", row.RuleId));

            window.TestView.DiagnosticsGrid.SelectedItem = rows[0];
            window.UpdateSelectedDiagnosticForTesting();
            Flush();
            Assert.Contains("OOP106", window.TestView.DetailRule.Text, StringComparison.Ordinal);
            StabilizeFixtureLocationForVisualBaseline(window, fixtureRoot, rows[0].FilePath);
            Capture(window, "filter-detail-oop106-ja", "ja");

            window.TestView.DangerFilter.IsChecked = true;
            window.TestView.WarningFilter.IsChecked = true;
            window.TestView.AttentionFilter.IsChecked = true;
            window.TestView.SearchFilter.Text = string.Empty;
            window.ApplyFiltersForTesting();
            window.SetLanguageForTesting(UserInterfaceLanguage.English);
            Flush();

            Assert.Equal(expectedCount, VisibleRows(window).Length);
            AssertSummaryMatches(window, result.Diagnostics);
            AssertMainWindowLayout(window);
            Capture(window, "fixture-en-default", "en");
        }
        finally
        {
            window.Close();
        }
    }

    [Avalonia.Headless.XUnit.AvaloniaFact]
    public void ErrorAndConfigurationEditorStatesRender()
    {
        var window = CreateWindow(1280, 820);
        try
        {
            const string error = "Release UI validation error";
            window.PresentErrorForTesting(error);
            Flush();

            Assert.Equal(error, window.TestView.Status.Text);
            Assert.Empty(VisibleRows(window));
            AssertMainWindowLayout(window);
            Capture(window, "error-ja-default", "ja");
        }
        finally
        {
            window.Close();
        }

        var editor = new ConfigurationEditorWindow(
            """
            {
              "disabledRules": ["OOP103"],
              "failureThreshold": "warning"
            }
            """,
            UserInterfaceLanguage.Japanese
        );
        editor.Show();
        try
        {
            Flush();
            Assert.True(editor.ClientSize.Width >= 560);
            Assert.True(editor.ClientSize.Height >= 420);
            Capture(editor, "config-editor-ja-default", "ja");
        }
        finally
        {
            editor.Close();
        }
    }

    private static void CaptureReadyState(string scenario, double width, double height)
    {
        var window = CreateWindow(width, height);
        try
        {
            Assert.Equal(ThemeVariant.Dark, Avalonia.Application.Current!.RequestedThemeVariant);
            Assert.Equal(ThemeVariant.Dark, window.ActualThemeVariant);
            Assert.Equal(0, window.TestView.ThemeSelector.SelectedIndex);
            AssertMainWindowLayout(window);
            Capture(window, scenario, "ja");
        }
        finally
        {
            window.Close();
        }
    }

    private static void PressKey(
        Window window,
        Key key,
        PhysicalKey physicalKey,
        RawInputModifiers modifiers = RawInputModifiers.None,
        string? keySymbol = null
    )
    {
        window.KeyPress(key, modifiers, physicalKey, keySymbol);
        window.KeyRelease(key, modifiers, physicalKey, keySymbol);
        Flush();
    }

    private static void WaitUntil(Func<bool> predicate, int timeoutMilliseconds)
    {
        var deadline = DateTime.UtcNow.AddMilliseconds(timeoutMilliseconds);
        while (DateTime.UtcNow < deadline)
        {
            Flush();
            if (predicate())
            {
                return;
            }

            Thread.Sleep(10);
        }

        Assert.True(predicate(), "Timed out waiting for UI state.");
    }

    private static MainWindow CreateWindow(double width, double height)
    {
        var window = new MainWindow { Width = width, Height = height };
        window.Show();
        Flush();
        return window;
    }

    private static void Flush()
    {
        Dispatcher.UIThread.RunJobs();
        AvaloniaHeadlessPlatform.ForceRenderTimerTick();
        Dispatcher.UIThread.RunJobs();
    }

    private static DiagnosticRow[] VisibleRows(MainWindow window)
    {
        var source = window.TestView.DiagnosticsGrid.ItemsSource as IEnumerable;
        return source?.Cast<object>().OfType<DiagnosticRow>().ToArray() ?? [];
    }

    private static void AssertSummaryMatches(
        MainWindow window,
        IReadOnlyList<DesignDiagnostic> diagnostics
    )
    {
        var danger = diagnostics.Count(item => item.Severity == DesignDiagnosticSeverity.Danger);
        var warning = diagnostics.Count(item => item.Severity == DesignDiagnosticSeverity.Warning);
        var attention = diagnostics.Count(item =>
            item.Severity == DesignDiagnosticSeverity.Attention
        );

        Assert.EndsWith($": {danger}", window.TestView.DangerCount.Text, StringComparison.Ordinal);
        Assert.EndsWith(
            $": {warning}",
            window.TestView.WarningCount.Text,
            StringComparison.Ordinal
        );
        Assert.EndsWith(
            $": {attention}",
            window.TestView.AttentionCount.Text,
            StringComparison.Ordinal
        );
    }

    private static void AssertMainWindowLayout(MainWindow window)
    {
        var controls = new Control[]
        {
            window.TestView.MainMenu,
            window.TestView.TargetPath,
            window.TestView.TargetFileButton,
            window.TestView.TargetFolderButton,
            window.TestView.ConfigurationPath,
            window.TestView.ConfigurationButton,
            window.TestView.EditConfigurationButton,
            window.TestView.ClearConfigurationButton,
            window.TestView.DangerFilter,
            window.TestView.WarningFilter,
            window.TestView.AttentionFilter,
            window.TestView.SearchFilter,
            window.TestView.AnalyzeButton,
            window.TestView.CancelButton,
            window.TestView.LanguageSelector,
            window.TestView.ThemeSelector,
            window.TestView.DiagnosticsGrid,
            window.TestView.Status,
        };

        foreach (var control in controls)
        {
            Assert.True(control.Bounds.Width > 0, $"{control.GetType().Name} has zero width.");
            Assert.True(control.Bounds.Height > 0, $"{control.GetType().Name} has zero height.");

            var origin = control.TranslatePoint(new Point(0, 0), window);
            Assert.True(origin.HasValue, $"{control.GetType().Name} could not be located.");
            var bounds = new Rect(origin!.Value, control.Bounds.Size);

            Assert.True(bounds.X >= -1, $"{control.GetType().Name} is clipped on the left.");
            Assert.True(bounds.Y >= -1, $"{control.GetType().Name} is clipped at the top.");
            Assert.True(
                bounds.Right <= window.ClientSize.Width + 1,
                $"{control.GetType().Name} is clipped on the right."
            );
            Assert.True(
                bounds.Bottom <= window.ClientSize.Height + 1,
                $"{control.GetType().Name} is clipped at the bottom."
            );
        }

        AssertNoOverlap(
            window,
            [
                window.TestView.DangerFilter,
                window.TestView.WarningFilter,
                window.TestView.AttentionFilter,
                window.TestView.SearchFilter,
                window.TestView.AnalyzeButton,
                window.TestView.CancelButton,
                window.TestView.LanguageSelector,
                window.TestView.ThemeSelector,
            ]
        );
    }

    private static void AssertNoOverlap(MainWindow window, IReadOnlyList<Control> controls)
    {
        for (var leftIndex = 0; leftIndex < controls.Count; leftIndex++)
        {
            var left = RelativeBounds(window, controls[leftIndex]);
            for (var rightIndex = leftIndex + 1; rightIndex < controls.Count; rightIndex++)
            {
                var right = RelativeBounds(window, controls[rightIndex]);
                var intersection = left.Intersect(right);
                Assert.True(
                    intersection.Width <= 1 || intersection.Height <= 1,
                    $"{controls[leftIndex].GetType().Name} overlaps {controls[rightIndex].GetType().Name}."
                );
            }
        }
    }

    private static Rect RelativeBounds(Window window, Control control)
    {
        var origin = control.TranslatePoint(new Point(0, 0), window);
        Assert.True(origin.HasValue);
        return new Rect(origin!.Value, control.Bounds.Size);
    }

    private static void Capture(Window window, string scenario, string language)
    {
        Flush();

        if (!VisualRegressionRequested())
        {
            return;
        }

        EnsureCanonicalVisualEnvironment();

        var frame = window.CaptureRenderedFrame();
        Assert.NotNull(frame);

        var baselinePath = Path.Combine(BaselineRoot(), $"{scenario}.png");
        var evidenceDirectory = Path.Combine(EvidenceRoot(), scenario);
        Directory.CreateDirectory(evidenceDirectory);

        var actualPath = Path.Combine(evidenceDirectory, "actual.png");
        var diffPath = Path.Combine(evidenceDirectory, "diff.png");
        frame.Save(actualPath, PngBitmapEncoderOptions.Default);

        WriteUiState(window, scenario, language, evidenceDirectory);

        if (Environment.GetEnvironmentVariable("UPDATE_VISUAL_BASELINES") == "1")
        {
            Directory.CreateDirectory(Path.GetDirectoryName(baselinePath)!);
            File.Copy(actualPath, baselinePath, overwrite: true);
            return;
        }

        Assert.True(File.Exists(baselinePath), $"Missing visual baseline: {baselinePath}");
        var changedRatio = CreateDiff(baselinePath, actualPath, diffPath);
        Assert.True(
            changedRatio <= MaximumChangedPixelRatio,
            $"Visual regression for {scenario}: {changedRatio:P3} pixels changed; "
                + $"threshold is {MaximumChangedPixelRatio:P3}. Diff: {diffPath}"
        );
    }

    private static void StabilizeFixtureLocationForVisualBaseline(
        MainWindow window,
        string fixtureRoot,
        string sourcePath
    )
    {
        if (!VisualRegressionRequested())
        {
            return;
        }

        // Golden images should validate UI layout, not the repository folder used to store
        // the fixture. Keep the pre-migration visual path stable so moving the checker
        // under tools/ does not create a false visual regression.
        var repositoryRoot = Path.GetFullPath(Path.Combine(RepositoryRoot(), "..", ".."));
        var relativePath = Path.GetRelativePath(fixtureRoot, sourcePath);
        var stablePath = Path.Combine(
            repositoryRoot,
            "tests",
            "fixtures",
            "release-validation-csharp",
            relativePath
        );
        window.TestView.DetailLocation.Text = (
            window.TestView.DetailLocation.Text ?? string.Empty
        ).Replace(sourcePath, stablePath, StringComparison.Ordinal);
    }

    private static bool VisualRegressionRequested() =>
        Environment.GetEnvironmentVariable(VisualRegressionEnvironmentVariable) == "1"
        || Environment.GetEnvironmentVariable("UPDATE_VISUAL_BASELINES") == "1";

    private static void EnsureCanonicalVisualEnvironment()
    {
        if (OperatingSystem.IsLinux())
        {
            return;
        }

        throw new InvalidOperationException(
            "Pixel visual regression is pinned to the canonical Linux/Skia environment. "
                + $"Run without {VisualRegressionEnvironmentVariable}=1 on Windows/macOS "
                + "to execute the platform-independent structural UI assertions only."
        );
    }

    private static double CreateDiff(string expectedPath, string actualPath, string diffPath)
    {
        using var expected = SKBitmap.Decode(expectedPath);
        using var actual = SKBitmap.Decode(actualPath);

        Assert.NotNull(expected);
        Assert.NotNull(actual);
        Assert.Equal(expected.Width, actual.Width);
        Assert.Equal(expected.Height, actual.Height);

        using var diff = new SKBitmap(
            actual.Width,
            actual.Height,
            SKColorType.Bgra8888,
            SKAlphaType.Premul
        );

        long changedPixels = 0;
        var totalPixels = (long)actual.Width * actual.Height;
        for (var y = 0; y < actual.Height; y++)
        {
            for (var x = 0; x < actual.Width; x++)
            {
                var expectedPixel = expected.GetPixel(x, y);
                var actualPixel = actual.GetPixel(x, y);
                var delta = Math.Max(
                    Math.Max(
                        Math.Abs(expectedPixel.Red - actualPixel.Red),
                        Math.Abs(expectedPixel.Green - actualPixel.Green)
                    ),
                    Math.Max(
                        Math.Abs(expectedPixel.Blue - actualPixel.Blue),
                        Math.Abs(expectedPixel.Alpha - actualPixel.Alpha)
                    )
                );

                if (delta > PixelDeltaThreshold)
                {
                    changedPixels++;
                    diff.SetPixel(x, y, SKColors.Red);
                }
                else
                {
                    diff.SetPixel(
                        x,
                        y,
                        new SKColor(actualPixel.Red, actualPixel.Green, actualPixel.Blue, 64)
                    );
                }
            }
        }

        using var image = SKImage.FromBitmap(diff);
        using var data = image.Encode(SKEncodedImageFormat.Png, 100);
        using var stream = File.Create(diffPath);
        data.SaveTo(stream);

        return totalPixels == 0 ? 0 : (double)changedPixels / totalPixels;
    }

    private static void WriteUiState(
        Window window,
        string scenario,
        string language,
        string evidenceDirectory
    )
    {
        var state = new
        {
            scenario,
            language,
            width = window.ClientSize.Width,
            height = window.ClientSize.Height,
            title = window.Title,
            theme = window.ActualThemeVariant.ToString(),
            visibleRowCount = window is MainWindow main ? VisibleRows(main).Length : (int?)null,
            status = window is MainWindow statusWindow ? statusWindow.TestView.Status.Text : null,
            selectedRule = window is MainWindow selectedWindow
            && selectedWindow.TestView.DiagnosticsGrid.SelectedItem is DiagnosticRow row
                ? row.RuleId
                : null,
            selectedFile = window is MainWindow selectedFileWindow
            && selectedFileWindow.TestView.DiagnosticsGrid.SelectedItem is DiagnosticRow fileRow
                ? fileRow.FileName
                : null,
            summary = window is MainWindow summaryWindow
                ? new
                {
                    danger = summaryWindow.TestView.DangerCount.Text,
                    warning = summaryWindow.TestView.WarningCount.Text,
                    attention = summaryWindow.TestView.AttentionCount.Text,
                    configuration = summaryWindow.TestView.ConfigurationSummary.Text,
                }
                : null,
        };

        File.WriteAllText(
            Path.Combine(evidenceDirectory, "ui-state.json"),
            JsonSerializer.Serialize(state, IndentedJson)
        );
    }

    private static int ReadExpectedDiagnosticCount(string fixtureRoot)
    {
        using var document = JsonDocument.Parse(
            File.ReadAllText(Path.Combine(fixtureRoot, "expected-diagnostics.json"))
        );
        return document.RootElement.GetProperty("exactDiagnostics").GetArrayLength();
    }

    private static string FixtureRoot() =>
        Path.Combine(RepositoryRoot(), "tests", "fixtures", "release-validation-csharp");

    private static string BaselineRoot() =>
        Path.Combine(RepositoryRoot(), "tests", "OopDesignChecker.Gui.HeadlessTests", "Baselines");

    private static string EvidenceRoot()
    {
        var configured = Environment.GetEnvironmentVariable("UI_EVIDENCE_DIR");
        return string.IsNullOrWhiteSpace(configured)
            ? Path.Combine(Path.GetTempPath(), "oop-design-checker-ui-evidence")
            : configured;
    }

    private static string RepositoryRoot()
    {
        foreach (
            var candidate in new[] { Directory.GetCurrentDirectory(), AppContext.BaseDirectory }
        )
        {
            var current = new DirectoryInfo(candidate);
            while (current is not null)
            {
                if (File.Exists(Path.Combine(current.FullName, "OopDesignChecker.sln")))
                {
                    return current.FullName;
                }

                current = current.Parent;
            }
        }

        throw new InvalidOperationException("Could not locate repository root.");
    }
}
