using OopDesignChecker.Core;

namespace OopDesignChecker.Analysis;

internal sealed class AnalysisEngine
{
    private readonly IReadOnlyList<IAnalysisRule> _rules;

    public AnalysisEngine(IReadOnlyList<IAnalysisRule> rules)
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
            .DistinctBy(CreateDiagnosticIdentity)
            .ToArray();

        cancellationToken.ThrowIfCancellationRequested();

        return DiagnosticCoordinator
            .Reduce(diagnostics)
            .OrderBy(diagnostic => diagnostic.Location.FilePath, StringComparer.OrdinalIgnoreCase)
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
            foreach (var diagnostic in rule.Analyze(context))
            {
                cancellationToken.ThrowIfCancellationRequested();
                yield return diagnostic;
            }
        }
    }

    private static DiagnosticIdentity CreateDiagnosticIdentity(DesignDiagnostic diagnostic) =>
        new(
            Path.GetFullPath(diagnostic.Location.FilePath).ToUpperInvariant(),
            diagnostic.Location.Line,
            diagnostic.Location.Column,
            diagnostic.Rule.Id,
            diagnostic.Message
        );

    private readonly record struct DiagnosticIdentity(
        string FilePath,
        int Line,
        int Column,
        string RuleId,
        string Message
    );
}
