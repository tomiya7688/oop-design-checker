using OopDesignChecker.Core;
using OopDesignChecker.Gui;
using OopDesignChecker.Localization;

namespace OopDesignChecker.Tests;

internal static class GUI表示スモークテスト
{
    public static void 実行する()
    {
        重要度で絞り込む();
        規則とファイルとシンボルと説明で絞り込む();
        診断行へ選択言語を適用する();
    }

    private static void 重要度で絞り込む()
    {
        var 行一覧 = new[]
        {
            表示行を作る(
                "OOP106",
                DesignDiagnosticSeverity.Danger,
                "danger.cs",
                "Mutable field",
                "DangerType"
            ),
            表示行を作る(
                "OOP305",
                DesignDiagnosticSeverity.Warning,
                "warning.cs",
                "Concrete dependency",
                "WarningType"
            ),
            表示行を作る(
                "OOP304",
                DesignDiagnosticSeverity.Attention,
                "attention.cs",
                "Deep inheritance",
                "AttentionType"
            ),
        };

        var 絞り込み結果 = DiagnosticFilter.Apply(
            行一覧,
            new DiagnosticFilterOptions(
                IncludeDanger: false,
                IncludeWarning: true,
                IncludeAttention: false,
                SearchText: string.Empty
            )
        );

        if (絞り込み結果.Count != 1 || 絞り込み結果[0].RuleId != "OOP305")
        {
            throw new InvalidOperationException("GUI severity filtering did not isolate warnings.");
        }
    }

    private static void 規則とファイルとシンボルと説明で絞り込む()
    {
        var 行一覧 = new[]
        {
            表示行を作る(
                "OOP106",
                DesignDiagnosticSeverity.Danger,
                "Player.cs",
                "Mutable field",
                "Player"
            ),
            表示行を作る(
                "OOP305",
                DesignDiagnosticSeverity.Warning,
                "ClockService.cs",
                "Concrete Clock dependency",
                "ClockService"
            ),
        };
        var オプション一覧 = new DiagnosticFilterOptions(true, true, true, "clock");
        var 絞り込み結果 = DiagnosticFilter.Apply(行一覧, オプション一覧);

        if (絞り込み結果.Count != 1 || 絞り込み結果[0].RuleId != "OOP305")
        {
            throw new InvalidOperationException(
                "GUI text filtering did not search diagnostic metadata."
            );
        }
    }

    private static void 診断行へ選択言語を適用する()
    {
        var 診断 = new DesignDiagnostic(
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

        var 日本語文言 = DiagnosticRow.From(診断, UserInterfaceLanguage.Japanese);
        var 英語文言 = DiagnosticRow.From(診断, UserInterfaceLanguage.English);

        if (
            日本語文言.RuleTitle != "継承階層が深すぎる"
            || !日本語文言.Message.Contains("プロジェクト内の継承階層", StringComparison.Ordinal)
            || 日本語文言.SeverityText != "注意"
            || 英語文言.RuleTitle != "Excessive inheritance depth"
            || 英語文言.Message != "Project-owned inheritance depth is 4."
            || 英語文言.SeverityText != "Attention"
        )
        {
            throw new InvalidOperationException(
                "GUI diagnostic rows did not follow the selected UI language."
            );
        }
    }

    private static DiagnosticRow 表示行を作る(
        string 規則ID,
        DesignDiagnosticSeverity 重要度,
        string ファイルパス,
        string メッセージ,
        string 記号名
    ) =>
        DiagnosticRow.From(
            new DesignDiagnostic(
                new RuleDescriptor(規則ID, 規則ID, 重要度),
                重要度,
                メッセージ,
                記号名,
                new SourceLocation(ファイルパス, 10, 5)
            )
        );
}
