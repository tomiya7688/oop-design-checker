using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using OopDesignChecker.Analysis;
using OopDesignChecker.Core;

namespace OopDesignChecker.Tests;

internal static class 診断協調スモークテスト
{
    public static void 実行する()
    {
        同一記号で不変条件迂回がカプセル化漏出を抑制する();
        同一記号で貧血オブジェクトが取得設定のみ警告を抑制する();
        優先規則を逆向きに適用しない();
        異なる記号を独立して扱う();
        無関係な規則の診断を残す();
        記号のない診断を抑制しない();
        メッセージ差だけで診断を重複させない();
        同じ位置の異なる記号を区別する();
        不明な診断パスを安定して扱う();
        パスの大文字小文字をOS別に比較する();
        パス規則に従って診断の抑制と順序を決める();
        診断順をパス規則で決める();
    }

    private static void 同一記号で不変条件迂回がカプセル化漏出を抑制する()
    {
        var 診断一覧 = 診断調整器.集約する([
            診断を作る("OOP106", DesignDiagnosticSeverity.Warning, "Account.Balance"),
            診断を作る("OOP107", DesignDiagnosticSeverity.Danger, "Account.Balance"),
        ]);

        規則IDを確認する(診断一覧, "OOP107");
    }

    private static void 同一記号で貧血オブジェクトが取得設定のみ警告を抑制する()
    {
        var 診断一覧 = 診断調整器.集約する([
            診断を作る("OOP405", DesignDiagnosticSeverity.Attention, "Order"),
            診断を作る("OOP402", DesignDiagnosticSeverity.Warning, "Order"),
        ]);

        規則IDを確認する(診断一覧, "OOP402");
    }

    private static void 優先規則を逆向きに適用しない()
    {
        var 診断一覧 = 診断調整器.集約する([
            診断を作る("OOP107", DesignDiagnosticSeverity.Danger, "Account.Balance"),
            診断を作る("OOP106", DesignDiagnosticSeverity.Warning, "Account.Other"),
        ]);

        規則IDを確認する(診断一覧, "OOP106", "OOP107");
    }

    private static void 異なる記号を独立して扱う()
    {
        var 診断一覧 = 診断調整器.集約する([
            診断を作る("OOP106", DesignDiagnosticSeverity.Warning, "Account.Name"),
            診断を作る("OOP107", DesignDiagnosticSeverity.Danger, "Account.Balance"),
        ]);

        規則IDを確認する(診断一覧, "OOP106", "OOP107");
    }

    private static void 無関係な規則の診断を残す()
    {
        var 診断一覧 = 診断調整器.集約する([
            診断を作る("OOP401", DesignDiagnosticSeverity.Warning, "Coordinator"),
            診断を作る("OOP403", DesignDiagnosticSeverity.Warning, "Coordinator"),
        ]);

        規則IDを確認する(診断一覧, "OOP401", "OOP403");
    }

    private static void 記号のない診断を抑制しない()
    {
        var 診断一覧 = 診断調整器.集約する([
            診断を作る("OOP106", DesignDiagnosticSeverity.Warning, null),
            診断を作る("OOP107", DesignDiagnosticSeverity.Danger, null),
        ]);

        規則IDを確認する(診断一覧, "OOP106", "OOP107");
    }

    private static void メッセージ差だけで診断を重複させない()
    {
        var 診断一覧 = エンジンを通して解析する(
            診断を作る("OOP999", DesignDiagnosticSeverity.Warning, "Order", "Original wording"),
            診断を作る("OOP999", DesignDiagnosticSeverity.Warning, "Order", "Localized wording")
        );

        if (診断一覧.Count == 1)
        {
            return;
        }

        throw new InvalidOperationException(
            $"Expected message-only diagnostic differences to collapse to one item, found {診断一覧.Count}."
        );
    }

    private static void 同じ位置の異なる記号を区別する()
    {
        var 診断一覧 = エンジンを通して解析する(
            診断を作る("OOP999", DesignDiagnosticSeverity.Warning, "Order.First"),
            診断を作る("OOP999", DesignDiagnosticSeverity.Warning, "Order.Second")
        );

        if (診断一覧.Count == 2)
        {
            return;
        }

        throw new InvalidOperationException(
            $"Expected different symbols at the same location to remain distinct, found {診断一覧.Count}."
        );
    }

    private static void 不明な診断パスを安定して扱う()
    {
        var 診断一覧 = エンジンを通して解析する(
            診断を作る(
                "OOP999",
                DesignDiagnosticSeverity.Warning,
                "Order",
                ファイルパス: "<unknown>"
            )
        );

        var 縮小後 = 診断調整器.集約する(診断一覧);
        if (縮小後.Count != 1 || 縮小後[0].Location.FilePath != "<unknown>")
        {
            throw new InvalidOperationException(
                "Unknown diagnostic paths must survive identity and coordination unchanged."
            );
        }
    }

    private static void パスの大文字小文字をOS別に比較する()
    {
        var 診断一覧 = エンジンを通して解析する(
            診断を作る(
                "OOP999",
                DesignDiagnosticSeverity.Warning,
                "Order",
                ファイルパス: "CaseSensitive.cs"
            ),
            診断を作る(
                "OOP999",
                DesignDiagnosticSeverity.Warning,
                "Order",
                ファイルパス: "casesensitive.cs"
            )
        );

        var 期待する件数 = OperatingSystem.IsWindows() ? 1 : 2;
        if (診断一覧.Count == 期待する件数)
        {
            return;
        }

        throw new InvalidOperationException(
            $"Expected {期待する件数} diagnostic(s) for path casing on {Environment.OSVersion.Platform}, found {診断一覧.Count}."
        );
    }

    private static void パス規則に従って診断の抑制と順序を決める()
    {
        var 大文字小文字が異なる同一パス = 診断調整器.集約する([
            診断を作る(
                "OOP106",
                DesignDiagnosticSeverity.Warning,
                "Account.Balance",
                ファイルパス: "CaseSensitive.cs"
            ),
            診断を作る(
                "OOP107",
                DesignDiagnosticSeverity.Danger,
                "Account.Balance",
                ファイルパス: "casesensitive.cs"
            ),
        ]);
        var 期待する同一パス件数 = OperatingSystem.IsWindows() ? 1 : 2;
        if (大文字小文字が異なる同一パス.Count != 期待する同一パス件数)
        {
            throw new InvalidOperationException(
                $"Expected {期待する同一パス件数} diagnostic(s) for path case comparison, found {大文字小文字が異なる同一パス.Count}."
            );
        }

        var 記号名の大文字小文字違い = 診断調整器.集約する([
            診断を作る("OOP106", DesignDiagnosticSeverity.Warning, "Account.Balance"),
            診断を作る("OOP107", DesignDiagnosticSeverity.Danger, "account.balance"),
        ]);
        規則IDを確認する(記号名の大文字小文字違い, "OOP106", "OOP107");
    }

    private static void 診断順をパス規則で決める()
    {
        var 診断一覧 = エンジンを通して解析する(
            診断を作る(
                "OOP999",
                DesignDiagnosticSeverity.Warning,
                "Order",
                ファイルパス: "CaseSensitive.cs",
                行: 2
            ),
            診断を作る(
                "OOP998",
                DesignDiagnosticSeverity.Warning,
                "Order",
                ファイルパス: "casesensitive.cs",
                行: 1
            )
        );

        var 期待する先頭パス = OperatingSystem.IsWindows()
            ? "casesensitive.cs"
            : "CaseSensitive.cs";
        if (Path.GetFileName(診断一覧[0].Location.FilePath) == 期待する先頭パス)
        {
            return;
        }

        throw new InvalidOperationException(
            $"Expected {期待する先頭パス} first under {Environment.OSVersion.Platform} path ordering, found {診断一覧[0].Location.FilePath}."
        );
    }

    private static IReadOnlyList<DesignDiagnostic> エンジンを通して解析する(
        params DesignDiagnostic[] 診断一覧
    )
    {
        var コンパイル結果 = CSharpCompilation.Create("diagnostic-identity");
        var プロジェクト = new 解析対象プロジェクト(
            Directory.GetCurrentDirectory(),
            false,
            コンパイル結果,
            new Dictionary<SyntaxTree, SemanticModel>()
        );
        var 解析器 = new 解析実行器([new 固定診断規則(診断一覧)]);
        return 解析器.解析する(プロジェクト);
    }

    private static DesignDiagnostic 診断を作る(
        string 規則ID,
        DesignDiagnosticSeverity 重要度,
        string? 記号名,
        string? メッセージ = null,
        string ファイルパス = "sample.cs",
        int 行 = 1
    ) =>
        new(
            new RuleDescriptor(規則ID, 規則ID, 重要度),
            重要度,
            メッセージ ?? 規則ID,
            記号名,
            new SourceLocation(ファイルパス, 行, 1)
        );

    private static void 規則IDを確認する(
        IReadOnlyList<DesignDiagnostic> 診断一覧,
        params string[] 期待する規則ID一覧
    )
    {
        var 実際の値 = 診断一覧.Select(診断 => 診断.Rule.Id).Order().ToArray();
        var 期待値 = 期待する規則ID一覧.Order().ToArray();
        if (実際の値.SequenceEqual(期待値, StringComparer.Ordinal))
        {
            return;
        }

        throw new InvalidOperationException(
            $"Expected [{string.Join(", ", 期待値)}], found [{string.Join(", ", 実際の値)}]."
        );
    }

    private sealed class 固定診断規則 : I解析規則
    {
        private readonly IReadOnlyList<DesignDiagnostic> _diagnostics;

        public 固定診断規則(IReadOnlyList<DesignDiagnostic> 診断一覧)
        {
            _diagnostics = 診断一覧;
            規則情報 = 診断一覧[0].Rule;
        }

        public RuleDescriptor 規則情報 { get; }

        public IEnumerable<DesignDiagnostic> 解析する(解析文脈 文脈) => _diagnostics;
    }
}
