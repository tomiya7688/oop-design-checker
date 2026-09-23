using System.Text.Json;
using OopDesignChecker.Configuration;
using OopDesignChecker.Core;
using OopDesignChecker.Gui;
using OopDesignChecker.Output;

namespace OopDesignChecker.Tests;

internal static class GuiWorkflowPhase2SmokeTests
{
    public static void Run()
    {
        ConfigurationJsonRoundTripsAndValidates();
        DiagnosticExportServiceWritesJsonAndSarif();
        CheckerServiceHonorsPreCancelledToken();
        ClipboardFailureIsContained();
        UiAutomationSettingsAreOptIn();
    }

    private static void ConfigurationJsonRoundTripsAndValidates()
    {
        var configuration = new CheckerConfiguration
        {
            IgnoredPaths = ["**/Generated/**"],
            DisabledRules = ["OOP103"],
            FailureThreshold = DesignDiagnosticSeverity.Warning,
            RuleSettings = new CheckerRuleSettings
            {
                Oop304 = new InheritanceDepthRuleSettings { WarningDepth = 6 },
            },
        };

        var json = CheckerConfigurationJson.Serialize(configuration);
        var parsed = CheckerConfigurationJson.Parse(json);
        if (
            parsed.FailureThreshold != DesignDiagnosticSeverity.Warning
            || parsed.IgnoredPaths.Count != 1
            || parsed.DisabledRules.Count != 1
            || parsed.RuleSettings.Oop304.WarningDepth != 6
        )
        {
            throw new InvalidOperationException(
                "Shared checker configuration JSON did not round-trip correctly."
            );
        }

        try
        {
            _ = CheckerConfigurationJson.Parse(
                """
                {
                  "ruleSettings": {
                    "oop304": {
                      "warningDepth": 0
                    }
                  }
                }
                """
            );
        }
        catch (InvalidOperationException exception)
            when (exception.Message.Contains("warningDepth", StringComparison.Ordinal))
        {
            return;
        }

        throw new InvalidOperationException(
            "Invalid OOP304 configuration was accepted by the shared JSON validator."
        );
    }

    private static void DiagnosticExportServiceWritesJsonAndSarif()
    {
        var temporaryDirectory = Path.Combine(
            Path.GetTempPath(),
            $"oop-checker-gui-export-{Guid.NewGuid():N}"
        );
        Directory.CreateDirectory(temporaryDirectory);

        try
        {
            var diagnostics = new[]
            {
                new DesignDiagnostic(
                    new RuleDescriptor(
                        "OOP999",
                        "Smoke diagnostic",
                        DesignDiagnosticSeverity.Warning
                    ),
                    DesignDiagnosticSeverity.Warning,
                    "Export smoke diagnostic.",
                    "Sample.Run",
                    new SourceLocation(Path.Combine(temporaryDirectory, "Sample.cs"), 3, 5)
                ),
            };
            var jsonPath = Path.Combine(temporaryDirectory, "result.json");
            var sarifPath = Path.Combine(temporaryDirectory, "result.sarif");

            DiagnosticExportService.Export(diagnostics, jsonPath, DiagnosticExportFormat.Json);
            DiagnosticExportService.Export(diagnostics, sarifPath, DiagnosticExportFormat.Sarif);

            using var jsonDocument = JsonDocument.Parse(File.ReadAllText(jsonPath));
            using var sarifDocument = JsonDocument.Parse(File.ReadAllText(sarifPath));
            var jsonRule = jsonDocument
                .RootElement.GetProperty("diagnostics")[0]
                .GetProperty("ruleId")
                .GetString();
            var sarifVersion = sarifDocument.RootElement.GetProperty("version").GetString();
            if (jsonRule != "OOP999" || sarifVersion != "2.1.0")
            {
                throw new InvalidOperationException(
                    "Shared diagnostic export did not produce the expected JSON/SARIF documents."
                );
            }
        }
        finally
        {
            Directory.Delete(temporaryDirectory, recursive: true);
        }
    }

    private static void UiAutomationSettingsAreOptIn()
    {
        const string delayVariable = "OOP_DESIGN_CHECKER_UI_AUTOMATION_ANALYSIS_DELAY_MS";
        const string exportVariable = "OOP_DESIGN_CHECKER_UI_AUTOMATION_EXPORT_DIR";
        var previousDelay = Environment.GetEnvironmentVariable(delayVariable);
        var previousExport = Environment.GetEnvironmentVariable(exportVariable);
        var temporaryDirectory = Path.Combine(
            Path.GetTempPath(),
            $"oop-checker-ui-automation-{Guid.NewGuid():N}"
        );

        try
        {
            Environment.SetEnvironmentVariable(delayVariable, null);
            Environment.SetEnvironmentVariable(exportVariable, null);
            if (
                UiAutomationSettings.AnalysisDelayMilliseconds != 0
                || UiAutomationSettings.ExportPath(DiagnosticExportFormat.Json) is not null
            )
            {
                throw new InvalidOperationException(
                    "GUI automation settings changed normal runtime behavior without opt-in."
                );
            }

            Environment.SetEnvironmentVariable(delayVariable, "2500");
            Environment.SetEnvironmentVariable(exportVariable, temporaryDirectory);
            var jsonPath = UiAutomationSettings.ExportPath(DiagnosticExportFormat.Json);
            var sarifPath = UiAutomationSettings.ExportPath(DiagnosticExportFormat.Sarif);
            if (
                UiAutomationSettings.AnalysisDelayMilliseconds != 2500
                || jsonPath != Path.Combine(temporaryDirectory, "actual-diagnostics.json")
                || sarifPath != Path.Combine(temporaryDirectory, "actual-diagnostics.sarif")
            )
            {
                throw new InvalidOperationException(
                    "GUI automation settings did not resolve deterministic test values."
                );
            }

            Environment.SetEnvironmentVariable(delayVariable, "30001");
            if (UiAutomationSettings.AnalysisDelayMilliseconds != 0)
            {
                throw new InvalidOperationException(
                    "GUI automation delay accepted an out-of-range value."
                );
            }
        }
        finally
        {
            Environment.SetEnvironmentVariable(delayVariable, previousDelay);
            Environment.SetEnvironmentVariable(exportVariable, previousExport);
        }
    }

    private static void ClipboardFailureIsContained()
    {
        var copied = ClipboardOperation
            .TrySetTextAsync(() =>
                Task.FromException(new InvalidOperationException("clipboard failed"))
            )
            .GetAwaiter()
            .GetResult();

        if (copied)
        {
            throw new InvalidOperationException(
                "ClipboardOperation reported success when the clipboard operation failed."
            );
        }
    }

    private static void CheckerServiceHonorsPreCancelledToken()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        try
        {
            _ = CheckerService.Analyze(
                Directory.GetCurrentDirectory(),
                cancellationToken: cancellation.Token
            );
        }
        catch (OperationCanceledException)
        {
            return;
        }

        throw new InvalidOperationException(
            "CheckerService ignored an already-cancelled analysis request."
        );
    }
}
