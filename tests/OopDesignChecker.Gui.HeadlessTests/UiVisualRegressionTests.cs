using System.Collections;
using System.Text.Json;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Threading;
using OopDesignChecker.Core;
using OopDesignChecker.Localization;
using SkiaSharp;
using Xunit;

namespace OopDesignChecker.Gui.HeadlessTests;

public sealed class UiVisualRegressionTests
{
    private const double MaximumChangedPixelRatio = 0.005;
    private const byte PixelDeltaThreshold = 12;

    [Avalonia.Headless.XUnit.AvaloniaFact]
    public void ReadyState_RendersJapaneseAtSupportedSizes()
    {
        CaptureReadyState("ready-ja-small", 900, 600);
        CaptureReadyState("ready-ja-default", 1280, 820);
        CaptureReadyState("ready-ja-large", 1600, 1000);
    }

    [Avalonia.Headless.XUnit.AvaloniaFact]
    public void EnglishState_RendersWithoutClipping()
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
    public void ReleaseFixture_RendersExpectedRowsFilterAndDetail()
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
    public void ErrorAndConfigurationEditorStates_Render()
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
            AssertMainWindowLayout(window);
            Capture(window, scenario, "ja");
        }
        finally
        {
            window.Close();
        }
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
        var attention = diagnostics.Count(
            item => item.Severity == DesignDiagnosticSeverity.Attention
        );

        Assert.EndsWith($": {danger}", window.TestView.DangerCount.Text, StringComparison.Ordinal);
        Assert.EndsWith($": {warning}", window.TestView.WarningCount.Text, StringComparison.Ordinal);
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
        var frame = window.CaptureRenderedFrame();
        Assert.NotNull(frame);

        var baselinePath = Path.Combine(BaselineRoot(), $"{scenario}.png");
        var evidenceDirectory = Path.Combine(EvidenceRoot(), scenario);
        Directory.CreateDirectory(evidenceDirectory);

        var actualPath = Path.Combine(evidenceDirectory, "actual.png");
        var diffPath = Path.Combine(evidenceDirectory, "diff.png");
        frame.Save(actualPath);

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
                        new SKColor(
                            actualPixel.Red,
                            actualPixel.Green,
                            actualPixel.Blue,
                            64
                        )
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
            visibleRowCount = window is MainWindow main ? VisibleRows(main).Length : (int?)null,
            status = window is MainWindow statusWindow ? statusWindow.TestView.Status.Text : null,
            selectedRule =
                window is MainWindow selectedWindow
                && selectedWindow.TestView.DiagnosticsGrid.SelectedItem is DiagnosticRow row
                    ? row.RuleId
                    : null,
            selectedFile =
                window is MainWindow selectedFileWindow
                && selectedFileWindow.TestView.DiagnosticsGrid.SelectedItem is DiagnosticRow fileRow
                    ? fileRow.FileName
                    : null,
            summary =
                window is MainWindow summaryWindow
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
            JsonSerializer.Serialize(state, new JsonSerializerOptions { WriteIndented = true })
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
        Path.Combine(
            RepositoryRoot(),
            "tests",
            "OopDesignChecker.Gui.HeadlessTests",
            "Baselines"
        );

    private static string EvidenceRoot()
    {
        var configured = Environment.GetEnvironmentVariable("UI_EVIDENCE_DIR");
        return string.IsNullOrWhiteSpace(configured)
            ? Path.Combine(Path.GetTempPath(), "oop-design-checker-ui-evidence")
            : configured;
    }

    private static string RepositoryRoot()
    {
        foreach (var candidate in new[] { Directory.GetCurrentDirectory(), AppContext.BaseDirectory })
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
