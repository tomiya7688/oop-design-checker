using System.Text.Json;
using OopDesignChecker.Configuration;
using OopDesignChecker.Core;
using OopDesignChecker.Output;

namespace OopDesignChecker.Tests;

internal static class GuiWorkflowPhase2SmokeTests
{
    public static void Run()
    {
        ConfigurationJsonRoundTripsAndValidates();
        DiagnosticExportServiceWritesJsonAndSarif();
        CheckerServiceHonorsPreCancelledToken();
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
            var jsonRule = jsonDocument.RootElement
                .GetProperty("diagnostics")[0]
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
