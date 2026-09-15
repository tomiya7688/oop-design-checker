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
    }

    private static void InvariantBypassSuppressesEncapsulationLeakForSameSymbol()
    {
        var diagnostics = DiagnosticCoordinator.Reduce(
            [
                Create("OOP106", DesignDiagnosticSeverity.Warning, "Account.Balance"),
                Create("OOP107", DesignDiagnosticSeverity.Danger, "Account.Balance"),
            ]
        );

        AssertRuleIds(diagnostics, "OOP107");
    }

    private static void AnemicObjectSuppressesGetterSetterOnlyForSameSymbol()
    {
        var diagnostics = DiagnosticCoordinator.Reduce(
            [
                Create("OOP405", DesignDiagnosticSeverity.Attention, "Order"),
                Create("OOP402", DesignDiagnosticSeverity.Warning, "Order"),
            ]
        );

        AssertRuleIds(diagnostics, "OOP402");
    }

    private static void DifferentSymbolsRemainIndependent()
    {
        var diagnostics = DiagnosticCoordinator.Reduce(
            [
                Create("OOP106", DesignDiagnosticSeverity.Warning, "Account.Name"),
                Create("OOP107", DesignDiagnosticSeverity.Danger, "Account.Balance"),
            ]
        );

        AssertRuleIds(diagnostics, "OOP106", "OOP107");
    }

    private static void UnrelatedRulesRemainTogether()
    {
        var diagnostics = DiagnosticCoordinator.Reduce(
            [
                Create("OOP401", DesignDiagnosticSeverity.Warning, "Coordinator"),
                Create("OOP403", DesignDiagnosticSeverity.Warning, "Coordinator"),
            ]
        );

        AssertRuleIds(diagnostics, "OOP401", "OOP403");
    }

    private static void DiagnosticsWithoutSymbolsAreNotSuppressed()
    {
        var diagnostics = DiagnosticCoordinator.Reduce(
            [
                Create("OOP106", DesignDiagnosticSeverity.Warning, null),
                Create("OOP107", DesignDiagnosticSeverity.Danger, null),
            ]
        );

        AssertRuleIds(diagnostics, "OOP106", "OOP107");
    }

    private static DesignDiagnostic Create(
        string ruleId,
        DesignDiagnosticSeverity severity,
        string? symbolName
    ) =>
        new(
            new RuleDescriptor(ruleId, ruleId, severity),
            severity,
            ruleId,
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
}
