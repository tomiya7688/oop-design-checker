using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using OopDesignChecker.Analysis;
using OopDesignChecker.Core;

namespace OopDesignChecker.Tests;

internal static class DiagnosticCoordinationSmokeTests
{
    public static void Run()
    {
        同一記号で不変条件迂回がカプセル化漏出を抑制する();
        同一記号で貧血オブジェクトが取得設定のみ警告を抑制する();
        DominanceDoesNotRunInReverse();
        DifferentSymbolsRemainIndependent();
        UnrelatedRulesRemainTogether();
        DiagnosticsWithoutSymbolsAreNotSuppressed();
        MessageChangesDoNotCreateDuplicateDiagnostics();
        DifferentSymbolsAtSameLocationRemainDistinct();
        不明な診断パスを安定して扱う();
        パスの大文字小文字をOS別に比較する();
        パス規則に従って診断の抑制と順序を決める();
        診断順をパス規則で決める();
    }

    private static void 同一記号で不変条件迂回がカプセル化漏出を抑制する()
    {
        var diagnostics = 診断調整器.集約する([
            Create("OOP106", DesignDiagnosticSeverity.Warning, "Account.Balance"),
            Create("OOP107", DesignDiagnosticSeverity.Danger, "Account.Balance"),
        ]);

        AssertRuleIds(diagnostics, "OOP107");
    }

    private static void 同一記号で貧血オブジェクトが取得設定のみ警告を抑制する()
    {
        var diagnostics = 診断調整器.集約する([
            Create("OOP405", DesignDiagnosticSeverity.Attention, "Order"),
            Create("OOP402", DesignDiagnosticSeverity.Warning, "Order"),
        ]);

        AssertRuleIds(diagnostics, "OOP402");
    }

    private static void DominanceDoesNotRunInReverse()
    {
        var diagnostics = 診断調整器.集約する([
            Create("OOP107", DesignDiagnosticSeverity.Danger, "Account.Balance"),
            Create("OOP106", DesignDiagnosticSeverity.Warning, "Account.Other"),
        ]);

        AssertRuleIds(diagnostics, "OOP106", "OOP107");
    }

    private static void DifferentSymbolsRemainIndependent()
    {
        var diagnostics = 診断調整器.集約する([
            Create("OOP106", DesignDiagnosticSeverity.Warning, "Account.Name"),
            Create("OOP107", DesignDiagnosticSeverity.Danger, "Account.Balance"),
        ]);

        AssertRuleIds(diagnostics, "OOP106", "OOP107");
    }

    private static void UnrelatedRulesRemainTogether()
    {
        var diagnostics = 診断調整器.集約する([
            Create("OOP401", DesignDiagnosticSeverity.Warning, "Coordinator"),
            Create("OOP403", DesignDiagnosticSeverity.Warning, "Coordinator"),
        ]);

        AssertRuleIds(diagnostics, "OOP401", "OOP403");
    }

    private static void DiagnosticsWithoutSymbolsAreNotSuppressed()
    {
        var diagnostics = 診断調整器.集約する([
            Create("OOP106", DesignDiagnosticSeverity.Warning, null),
            Create("OOP107", DesignDiagnosticSeverity.Danger, null),
        ]);

        AssertRuleIds(diagnostics, "OOP106", "OOP107");
    }

    private static void MessageChangesDoNotCreateDuplicateDiagnostics()
    {
        var diagnostics = エンジンを通して解析する(
            Create("OOP999", DesignDiagnosticSeverity.Warning, "Order", "Original wording"),
            Create("OOP999", DesignDiagnosticSeverity.Warning, "Order", "Localized wording")
        );

        if (diagnostics.Count == 1)
        {
            return;
        }

        throw new InvalidOperationException(
            $"Expected message-only diagnostic differences to collapse to one item, found {diagnostics.Count}."
        );
    }

    private static void DifferentSymbolsAtSameLocationRemainDistinct()
    {
        var diagnostics = エンジンを通して解析する(
            Create("OOP999", DesignDiagnosticSeverity.Warning, "Order.First"),
            Create("OOP999", DesignDiagnosticSeverity.Warning, "Order.Second")
        );

        if (diagnostics.Count == 2)
        {
            return;
        }

        throw new InvalidOperationException(
            $"Expected different symbols at the same location to remain distinct, found {diagnostics.Count}."
        );
    }

    private static void 不明な診断パスを安定して扱う()
    {
        var diagnostics = エンジンを通して解析する(
            Create("OOP999", DesignDiagnosticSeverity.Warning, "Order", filePath: "<unknown>")
        );

        var reduced = 診断調整器.集約する(diagnostics);
        if (reduced.Count != 1 || reduced[0].Location.FilePath != "<unknown>")
        {
            throw new InvalidOperationException(
                "Unknown diagnostic paths must survive identity and coordination unchanged."
            );
        }
    }

    private static void パスの大文字小文字をOS別に比較する()
    {
        var diagnostics = エンジンを通して解析する(
            Create(
                "OOP999",
                DesignDiagnosticSeverity.Warning,
                "Order",
                filePath: "CaseSensitive.cs"
            ),
            Create(
                "OOP999",
                DesignDiagnosticSeverity.Warning,
                "Order",
                filePath: "casesensitive.cs"
            )
        );

        var expectedCount = OperatingSystem.IsWindows() ? 1 : 2;
        if (diagnostics.Count == expectedCount)
        {
            return;
        }

        throw new InvalidOperationException(
            $"Expected {expectedCount} diagnostic(s) for path casing on {Environment.OSVersion.Platform}, found {diagnostics.Count}."
        );
    }

    private static void パス規則に従って診断の抑制と順序を決める()
    {
        var samePathWithDifferentCasing = 診断調整器.集約する([
            Create(
                "OOP106",
                DesignDiagnosticSeverity.Warning,
                "Account.Balance",
                filePath: "CaseSensitive.cs"
            ),
            Create(
                "OOP107",
                DesignDiagnosticSeverity.Danger,
                "Account.Balance",
                filePath: "casesensitive.cs"
            ),
        ]);
        var expectedSamePathCount = OperatingSystem.IsWindows() ? 1 : 2;
        if (samePathWithDifferentCasing.Count != expectedSamePathCount)
        {
            throw new InvalidOperationException(
                $"Expected {expectedSamePathCount} diagnostic(s) for path case comparison, found {samePathWithDifferentCasing.Count}."
            );
        }

        var differentSymbolCasing = 診断調整器.集約する([
            Create("OOP106", DesignDiagnosticSeverity.Warning, "Account.Balance"),
            Create("OOP107", DesignDiagnosticSeverity.Danger, "account.balance"),
        ]);
        AssertRuleIds(differentSymbolCasing, "OOP106", "OOP107");
    }

    private static void 診断順をパス規則で決める()
    {
        var diagnostics = エンジンを通して解析する(
            Create(
                "OOP999",
                DesignDiagnosticSeverity.Warning,
                "Order",
                filePath: "CaseSensitive.cs",
                line: 2
            ),
            Create(
                "OOP998",
                DesignDiagnosticSeverity.Warning,
                "Order",
                filePath: "casesensitive.cs",
                line: 1
            )
        );

        var expectedFirstPath = OperatingSystem.IsWindows()
            ? "casesensitive.cs"
            : "CaseSensitive.cs";
        if (Path.GetFileName(diagnostics[0].Location.FilePath) == expectedFirstPath)
        {
            return;
        }

        throw new InvalidOperationException(
            $"Expected {expectedFirstPath} first under {Environment.OSVersion.Platform} path ordering, found {diagnostics[0].Location.FilePath}."
        );
    }

    private static IReadOnlyList<DesignDiagnostic> エンジンを通して解析する(
        params DesignDiagnostic[] diagnostics
    )
    {
        var compilation = CSharpCompilation.Create("diagnostic-identity");
        var project = new 解析対象プロジェクト(
            Directory.GetCurrentDirectory(),
            false,
            compilation,
            new Dictionary<SyntaxTree, SemanticModel>()
        );
        var engine = new 解析実行器([new FixedDiagnosticsRule(diagnostics)]);
        return engine.解析する(project);
    }

    private static DesignDiagnostic Create(
        string ruleId,
        DesignDiagnosticSeverity severity,
        string? symbolName,
        string? message = null,
        string filePath = "sample.cs",
        int line = 1
    ) =>
        new(
            new RuleDescriptor(ruleId, ruleId, severity),
            severity,
            message ?? ruleId,
            symbolName,
            new SourceLocation(filePath, line, 1)
        );

    private static void AssertRuleIds(
        IReadOnlyList<DesignDiagnostic> diagnostics,
        params string[] expectedRuleIds
    )
    {
        var actual = diagnostics.Select(diagnostic => diagnostic.Rule.Id).Order().ToArray();
        var expected = expectedRuleIds.Order().ToArray();
        if (actual.SequenceEqual(expected, StringComparer.Ordinal))
        {
            return;
        }

        throw new InvalidOperationException(
            $"Expected [{string.Join(", ", expected)}], found [{string.Join(", ", actual)}]."
        );
    }

    private sealed class FixedDiagnosticsRule : I解析規則
    {
        private readonly IReadOnlyList<DesignDiagnostic> _diagnostics;

        public FixedDiagnosticsRule(IReadOnlyList<DesignDiagnostic> diagnostics)
        {
            _diagnostics = diagnostics;
            規則情報 = diagnostics[0].Rule;
        }

        public RuleDescriptor 規則情報 { get; }

        public IEnumerable<DesignDiagnostic> 解析する(解析文脈 context) => _diagnostics;
    }
}
