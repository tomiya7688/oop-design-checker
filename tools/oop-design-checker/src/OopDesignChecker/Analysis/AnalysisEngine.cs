using OopDesignChecker.Core;

namespace OopDesignChecker.Analysis;

internal sealed class AnalysisEngine
{
    private readonly IReadOnlyList<I解析規則> _rules;

    public AnalysisEngine(IReadOnlyList<I解析規則> rules)
    {
        _rules = rules;
    }

    public IReadOnlyList<DesignDiagnostic> Analyze(
        SourceProject project,
        CancellationToken cancellationToken = default
    ) => Analyze([project], cancellationToken);

    public IReadOnlyList<DesignDiagnostic> Analyze(
        IReadOnlyList<SourceProject> projects,
        CancellationToken cancellationToken = default
    )
    {
        cancellationToken.ThrowIfCancellationRequested();

        var diagnostics = projects
            .SelectMany(project => AnalyzeProject(project, projects, cancellationToken))
            .DistinctBy(CreateDiagnosticIdentity, DiagnosticIdentityComparer.Instance)
            .ToArray();

        cancellationToken.ThrowIfCancellationRequested();

        return DiagnosticCoordinator
            .Reduce(diagnostics)
            .OrderBy(
                diagnostic => 診断ファイルパス.正規化する(diagnostic.Location.FilePath),
                パス比較規則.文字列比較子
            )
            .ThenBy(diagnostic => diagnostic.Location.Line)
            .ThenBy(diagnostic => diagnostic.Location.Column)
            .ThenBy(diagnostic => diagnostic.Rule.Id, StringComparer.Ordinal)
            .ToArray();
    }

    private IEnumerable<DesignDiagnostic> AnalyzeProject(
        SourceProject project,
        IReadOnlyList<SourceProject> projects,
        CancellationToken cancellationToken
    )
    {
        var context = new AnalysisContext(project, projects, cancellationToken);
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

    private static DiagnosticIdentity CreateDiagnosticIdentity(DesignDiagnostic diagnostic) =>
        new(
            診断ファイルパス.正規化する(diagnostic.Location.FilePath),
            diagnostic.Location.Line,
            diagnostic.Location.Column,
            diagnostic.Rule.Id,
            diagnostic.SymbolName
        );

    private readonly record struct DiagnosticIdentity(
        string FilePath,
        int Line,
        int Column,
        string RuleId,
        string? SymbolName
    );

    private sealed class DiagnosticIdentityComparer : IEqualityComparer<DiagnosticIdentity>
    {
        public static DiagnosticIdentityComparer Instance { get; } = new();

        public bool Equals(DiagnosticIdentity left, DiagnosticIdentity right) =>
            パス比較規則.文字列比較子.Equals(left.FilePath, right.FilePath)
            && left.Line == right.Line
            && left.Column == right.Column
            && string.Equals(left.RuleId, right.RuleId, StringComparison.Ordinal)
            && string.Equals(left.SymbolName, right.SymbolName, StringComparison.Ordinal);

        public int GetHashCode(DiagnosticIdentity identity)
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
