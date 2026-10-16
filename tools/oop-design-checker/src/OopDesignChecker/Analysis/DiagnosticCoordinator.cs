using System.Collections.Frozen;
using OopDesignChecker.Core;

namespace OopDesignChecker.Analysis;

internal static class 診断調整器
{
    // {
    //   責務: 診断の優先関係を明示します,
    //   処理: [
    //     1: 同じシンボルを対象とする場合に、強い診断が補助的な診断を抑制します,
    //     2: 重要度だけで診断の優先関係を決めません
    //   ]
    // }
    private static readonly FrozenDictionary<string, string> 包含先規則対応表 = new Dictionary<
        string,
        string
    >(StringComparer.Ordinal)
    {
        ["OOP106"] = "OOP107",
        ["OOP405"] = "OOP402",
    }.ToFrozenDictionary(StringComparer.Ordinal);

    public static IReadOnlyList<DesignDiagnostic> 集約する(
        IReadOnlyList<DesignDiagnostic> diagnostics
    )
    {
        var diagnosticKeys = diagnostics
            .Where(diagnostic => diagnostic.SymbolName is not null)
            .Select(規則記号識別子を作る)
            .ToHashSet(規則記号識別子比較器.Instance);

        return diagnostics
            .Where(diagnostic => !抑制対象である(diagnostic, diagnosticKeys))
            .ToArray();
    }

    private static bool 抑制対象である(
        DesignDiagnostic diagnostic,
        IReadOnlySet<規則記号識別子> diagnosticKeys
    )
    {
        if (
            diagnostic.SymbolName is null
            || !包含先規則対応表.TryGetValue(diagnostic.Rule.Id, out var dominantRuleId)
        )
        {
            return false;
        }

        return diagnosticKeys.Contains(規則記号識別子を作る(diagnostic, dominantRuleId));
    }

    private static 規則記号識別子 規則記号識別子を作る(DesignDiagnostic diagnostic) =>
        規則記号識別子を作る(diagnostic, diagnostic.Rule.Id);

    private static 規則記号識別子 規則記号識別子を作る(
        DesignDiagnostic diagnostic,
        string ruleId
    ) => new(パスを正規化する(diagnostic.Location.FilePath), diagnostic.SymbolName!, ruleId);

    private static string パスを正規化する(string パス) => 診断ファイルパス.正規化する(パス);

    private readonly record struct 規則記号識別子(
        string FilePath,
        string SymbolName,
        string RuleId
    );

    private sealed class 規則記号識別子比較器 : IEqualityComparer<規則記号識別子>
    {
        public static 規則記号識別子比較器 Instance { get; } = new();

        public bool Equals(規則記号識別子 left, 規則記号識別子 right) =>
            パス比較規則.文字列比較子.Equals(left.FilePath, right.FilePath)
            && string.Equals(left.SymbolName, right.SymbolName, StringComparison.Ordinal)
            && string.Equals(left.RuleId, right.RuleId, StringComparison.Ordinal);

        public int GetHashCode(規則記号識別子 key)
        {
            var hash = new HashCode();
            hash.Add(key.FilePath, パス比較規則.文字列比較子);
            hash.Add(key.SymbolName, StringComparer.Ordinal);
            hash.Add(key.RuleId, StringComparer.Ordinal);
            return hash.ToHashCode();
        }
    }
}
