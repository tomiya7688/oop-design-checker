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
        DifferentSymbolsRemainIndependent();
        UnrelatedRulesRemainTogether();
        DiagnosticsWithoutSymbolsAreNotSuppressed();
        MessageChangesDoNotCreateDuplicateDiagnostics();
        DifferentSymbolsAtSameLocationRemainDistinct();
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
        string? message = null
    ) =>
        new(
            new RuleDescriptor(ruleId, ruleId, severity),
            severity,
            message ?? ruleId,
            symbolName,
            new SourceLocation("sample.cs", 1, 1)
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

    private sealed class FixedDiagnosticsRule : IAnalysisRule
    {
        private readonly IReadOnlyList<DesignDiagnostic> _diagnostics;

        public FixedDiagnosticsRule(IReadOnlyList<DesignDiagnostic> diagnostics)
        {
            _diagnostics = diagnostics;
            Descriptor = diagnostics[0].Rule;
        }

        public RuleDescriptor Descriptor { get; }

        public IEnumerable<DesignDiagnostic> Analyze(AnalysisContext context) => _diagnostics;
    }
}
