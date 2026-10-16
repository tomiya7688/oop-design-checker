using OopDesignChecker.Core;

namespace OopDesignChecker.Analysis;

internal sealed class 解析実行器
{
    private readonly IReadOnlyList<I解析規則> _rules;

    public 解析実行器(IReadOnlyList<I解析規則> rules)
    {
        _rules = rules;
    }

    public IReadOnlyList<DesignDiagnostic> 解析する(
        解析対象プロジェクト project,
        CancellationToken cancellationToken = default
    ) => 解析する([project], cancellationToken);

    public IReadOnlyList<DesignDiagnostic> 解析する(
        IReadOnlyList<解析対象プロジェクト> projects,
        CancellationToken cancellationToken = default
    )
    {
        cancellationToken.ThrowIfCancellationRequested();

        var diagnostics = projects
            .SelectMany(project => プロジェクトを解析する(project, projects, cancellationToken))
            .DistinctBy(診断識別情報を作る, 診断識別情報比較器.Instance)
            .ToArray();

        cancellationToken.ThrowIfCancellationRequested();

        return 診断調整器
            .集約する(diagnostics)
            .OrderBy(
                diagnostic => 診断ファイルパス.正規化する(diagnostic.Location.FilePath),
                パス比較規則.文字列比較子
            )
            .ThenBy(diagnostic => diagnostic.Location.Line)
            .ThenBy(diagnostic => diagnostic.Location.Column)
            .ThenBy(diagnostic => diagnostic.Rule.Id, StringComparer.Ordinal)
            .ToArray();
    }

    private IEnumerable<DesignDiagnostic> プロジェクトを解析する(
        解析対象プロジェクト project,
        IReadOnlyList<解析対象プロジェクト> projects,
        CancellationToken cancellationToken
    )
    {
        var context = new 解析文脈(project, projects, cancellationToken);
        foreach (var rule in _rules)
        {
            cancellationToken.ThrowIfCancellationRequested();
            foreach (var diagnostic in rule.解析する(context))
            {
                cancellationToken.ThrowIfCancellationRequested();
                yield return diagnostic;
            }
        }
    }

    private static 診断識別情報 診断識別情報を作る(DesignDiagnostic diagnostic) =>
        new(
            診断ファイルパス.正規化する(diagnostic.Location.FilePath),
            diagnostic.Location.Line,
            diagnostic.Location.Column,
            diagnostic.Rule.Id,
            diagnostic.SymbolName
        );

    private readonly record struct 診断識別情報(
        string FilePath,
        int Line,
        int Column,
        string RuleId,
        string? SymbolName
    );

    private sealed class 診断識別情報比較器 : IEqualityComparer<診断識別情報>
    {
        public static 診断識別情報比較器 Instance { get; } = new();

        public bool Equals(診断識別情報 left, 診断識別情報 right) =>
            パス比較規則.文字列比較子.Equals(left.FilePath, right.FilePath)
            && left.Line == right.Line
            && left.Column == right.Column
            && string.Equals(left.RuleId, right.RuleId, StringComparison.Ordinal)
            && string.Equals(left.SymbolName, right.SymbolName, StringComparison.Ordinal);

        public int GetHashCode(診断識別情報 identity)
        {
            var hash = new HashCode();
            hash.Add(identity.FilePath, パス比較規則.文字列比較子);
            hash.Add(identity.Line);
            hash.Add(identity.Column);
            hash.Add(identity.RuleId, StringComparer.Ordinal);
            hash.Add(identity.SymbolName, StringComparer.Ordinal);
            return hash.ToHashCode();
        }
    }
}
