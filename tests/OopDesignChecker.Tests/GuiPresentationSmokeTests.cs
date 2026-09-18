using OopDesignChecker.Core;
using OopDesignChecker.Gui;
using OopDesignChecker.Localization;

namespace OopDesignChecker.Tests;

internal static class GuiPresentationSmokeTests
{
    public static void Run()
    {
        FiltersBySeverity();
        FiltersByRuleFileSymbolAndMessage();
        DiagnosticRowsFollowSelectedLanguage();
    }

    private static void FiltersBySeverity()
    {
        var rows = new[]
        {
            CreateRow(
                "OOP106",
                DesignDiagnosticSeverity.Danger,
                "danger.cs",
                "Mutable field",
                "DangerType"
            ),
            CreateRow(
                "OOP305",
                DesignDiagnosticSeverity.Warning,
                "warning.cs",
                "Concrete dependency",
                "WarningType"
            ),
            CreateRow(
                "OOP304",
                DesignDiagnosticSeverity.Attention,
                "attention.cs",
                "Deep inheritance",
                "AttentionType"
            ),
        };

        var filtered = DiagnosticFilter.Apply(
            rows,
            new DiagnosticFilterOptions(
                IncludeDanger: false,
                IncludeWarning: true,
                IncludeAttention: false,
                SearchText: string.Empty
            )
        );

        if (filtered.Count != 1 || filtered[0].RuleId != "OOP305")
        {
            throw new InvalidOperationException("GUI severity filtering did not isolate warnings.");
        }
    }

    private static void FiltersByRuleFileSymbolAndMessage()
    {
        var rows = new[]
        {
            CreateRow(
                "OOP106",
                DesignDiagnosticSeverity.Danger,
                "Player.cs",
                "Mutable field",
                "Player"
            ),
            CreateRow(
                "OOP305",
                DesignDiagnosticSeverity.Warning,
                "ClockService.cs",
                "Concrete Clock dependency",
                "ClockService"
            ),
        };
        var options = new DiagnosticFilterOptions(true, true, true, "clock");
        var filtered = DiagnosticFilter.Apply(rows, options);

        if (filtered.Count != 1 || filtered[0].RuleId != "OOP305")
        {
            throw new InvalidOperationException(
                "GUI text filtering did not search diagnostic metadata."
            );
        }
    }

    private static void DiagnosticRowsFollowSelectedLanguage()
    {
        var diagnostic = new DesignDiagnostic(
            new RuleDescriptor(
                "OOP304",
                "Excessive inheritance depth",
                DesignDiagnosticSeverity.Attention
            ),
            DesignDiagnosticSeverity.Attention,
            "Project-owned inheritance depth is 4.",
            "Sample.DeepType",
            new SourceLocation("deep.cs", 12, 3)
        );

        var japanese = DiagnosticRow.From(diagnostic, UserInterfaceLanguage.Japanese);
        var english = DiagnosticRow.From(diagnostic, UserInterfaceLanguage.English);

        if (
            japanese.RuleTitle != "継承階層が深すぎる"
            || !japanese.Message.Contains("プロジェクト内の継承階層", StringComparison.Ordinal)
            || japanese.SeverityText != "注意"
            || english.RuleTitle != "Excessive inheritance depth"
            || english.Message != "Project-owned inheritance depth is 4."
            || english.SeverityText != "Attention"
        )
        {
            throw new InvalidOperationException(
                "GUI diagnostic rows did not follow the selected UI language."
            );
        }
    }

    private static DiagnosticRow CreateRow(
        string ruleId,
        DesignDiagnosticSeverity severity,
        string filePath,
        string message,
        string symbolName
    ) =>
        DiagnosticRow.From(
            new DesignDiagnostic(
                new RuleDescriptor(ruleId, ruleId, severity),
                severity,
                message,
                symbolName,
                new SourceLocation(filePath, 10, 5)
            )
        );
}
