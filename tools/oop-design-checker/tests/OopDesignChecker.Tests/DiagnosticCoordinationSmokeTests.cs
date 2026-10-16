using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using OopDesignChecker.Analysis;
using OopDesignChecker.Core;

namespace OopDesignChecker.Tests;

internal static class DiagnosticCoordinationSmokeTests
{
    public static void Run()
    {
        InvariantBypassSuppressesEncapsulationLeakForSameSymbol();
        AnemicObjectSuppressesGetterSetterOnlyForSameSymbol();
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

    private static void InvariantBypassSuppressesEncapsulationLeakForSameSymbol()
    {
        var diagnostics = DiagnosticCoordinator.Reduce([
            Create("OOP106", DesignDiagnosticSeverity.Warning, "Account.Balance"),
            Create("OOP107", DesignDiagnosticSeverity.Danger, "Account.Balance"),
        ]);

        AssertRuleIds(diagnostics, "OOP107");
    }

    private static void AnemicObjectSuppressesGetterSetterOnlyForSameSymbol()
    {
        var diagnostics = DiagnosticCoordinator.Reduce([
            Create("OOP405", DesignDiagnosticSeverity.Attention, "Order"),
            Create("OOP402", DesignDiagnosticSeverity.Warning, "Order"),
        ]);

        AssertRuleIds(diagnostics, "OOP402");
    }

    private static void DominanceDoesNotRunInReverse()
    {
        var diagnostics = DiagnosticCoordinator.Reduce([
            Create("OOP107", DesignDiagnosticSeverity.Danger, "Account.Balance"),
            Create("OOP106", DesignDiagnosticSeverity.Warning, "Account.Other"),
        ]);

        AssertRuleIds(diagnostics, "OOP106", "OOP107");
    }

    private static void DifferentSymbolsRemainIndependent()
    {
        var diagnostics = DiagnosticCoordinator.Reduce([
            Create("OOP106", DesignDiagnosticSeverity.Warning, "Account.Name"),
            Create("OOP107", DesignDiagnosticSeverity.Danger, "Account.Balance"),
        ]);

        AssertRuleIds(diagnostics, "OOP106", "OOP107");
    }

    private static void UnrelatedRulesRemainTogether()
    {
        var diagnostics = DiagnosticCoordinator.Reduce([
            Create("OOP401", DesignDiagnosticSeverity.Warning, "Coordinator"),
            Create("OOP403", DesignDiagnosticSeverity.Warning, "Coordinator"),
        ]);

        AssertRuleIds(diagnostics, "OOP401", "OOP403");
    }

    private static void DiagnosticsWithoutSymbolsAreNotSuppressed()
    {
        var diagnostics = DiagnosticCoordinator.Reduce([
            Create("OOP106", DesignDiagnosticSeverity.Warning, null),
            Create("OOP107", DesignDiagnosticSeverity.Danger, null),
        ]);

        AssertRuleIds(diagnostics, "OOP106", "OOP107");
    }

    private static void MessageChangesDoNotCreateDuplicateDiagnostics()
    {
        var diagnostics = AnalyzeThroughEngine(
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
        var diagnostics = AnalyzeThroughEngine(
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
        var diagnostics = AnalyzeThroughEngine(
            Create("OOP999", DesignDiagnosticSeverity.Warning, "Order", filePath: "<unknown>")
        );

        var reduced = DiagnosticCoordinator.Reduce(diagnostics);
        if (reduced.Count != 1 || reduced[0].Location.FilePath != "<unknown>")
        {
            throw new InvalidOperationException(
                "Unknown diagnostic paths must survive identity and coordination unchanged."
            );
        }
    }

    private static void パスの大文字小文字をOS別に比較する()
    {
        var diagnostics = AnalyzeThroughEngine(
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
        var samePathWithDifferentCasing = DiagnosticCoordinator.Reduce([
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

        var differentSymbolCasing = DiagnosticCoordinator.Reduce([
            Create("OOP106", DesignDiagnosticSeverity.Warning, "Account.Balance"),
            Create("OOP107", DesignDiagnosticSeverity.Danger, "account.balance"),
        ]);
        AssertRuleIds(differentSymbolCasing, "OOP106", "OOP107");
    }

    private static void 診断順をパス規則で決める()
    {
        var diagnostics = AnalyzeThroughEngine(
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

    private static IReadOnlyList<DesignDiagnostic> AnalyzeThroughEngine(
        params DesignDiagnostic[] diagnostics
    )
    {
        var compilation = CSharpCompilation.Create("diagnostic-identity");
        var project = new SourceProject(
            Directory.GetCurrentDirectory(),
            false,
            compilation,
            new Dictionary<SyntaxTree, SemanticModel>()
        );
        var engine = new AnalysisEngine([new FixedDiagnosticsRule(diagnostics)]);
        return engine.Analyze(project);
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

        public IEnumerable<DesignDiagnostic> 解析する(AnalysisContext context) => _diagnostics;
    }
}
