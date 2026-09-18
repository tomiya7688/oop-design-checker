using System.Collections.Frozen;
using OopDesignChecker.Core;

namespace OopDesignChecker.Analysis;

internal static class DiagnosticCoordinator
{
    // Dominance is intentionally explicit: a stronger diagnostic suppresses a weaker supporting
    // signal only when both refer to the same symbol. Severity alone never implies dominance.
    private static readonly FrozenDictionary<string, string> DominantRuleBySubsumedRule =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["OOP106"] = "OOP107",
            ["OOP405"] = "OOP402",
        }.ToFrozenDictionary(StringComparer.Ordinal);

    public static IReadOnlyList<DesignDiagnostic> Reduce(
        IReadOnlyList<DesignDiagnostic> diagnostics
    )
    {
        var diagnosticKeys = diagnostics
            .Where(diagnostic => diagnostic.SymbolName is not null)
            .Select(CreateRuleSymbolKey)
            .ToHashSet();

        return diagnostics.Where(diagnostic => !IsSubsumed(diagnostic, diagnosticKeys)).ToArray();
    }

    private static bool IsSubsumed(
        DesignDiagnostic diagnostic,
        IReadOnlySet<RuleSymbolKey> diagnosticKeys
    )
    {
        if (
            diagnostic.SymbolName is null
            || !DominantRuleBySubsumedRule.TryGetValue(diagnostic.Rule.Id, out var dominantRuleId)
        )
        {
            return false;
        }

        return diagnosticKeys.Contains(CreateRuleSymbolKey(diagnostic, dominantRuleId));
    }

    private static RuleSymbolKey CreateRuleSymbolKey(DesignDiagnostic diagnostic) =>
        CreateRuleSymbolKey(diagnostic, diagnostic.Rule.Id);

    private static RuleSymbolKey CreateRuleSymbolKey(DesignDiagnostic diagnostic, string ruleId) =>
        new(NormalizePath(diagnostic.Location.FilePath), diagnostic.SymbolName!, ruleId);

    private static string NormalizePath(string path) => Path.GetFullPath(path).ToUpperInvariant();

    private readonly record struct RuleSymbolKey(string FilePath, string SymbolName, string RuleId);
}
