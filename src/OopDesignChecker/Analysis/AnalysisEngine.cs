using OopDesignChecker.Core;

namespace OopDesignChecker.Analysis;

internal sealed class AnalysisEngine
{
    private readonly IReadOnlyList<IAnalysisRule> _rules;

    public AnalysisEngine(IReadOnlyList<IAnalysisRule> rules)
    {
        _rules = rules;
    }

    public IReadOnlyList<DesignDiagnostic> Analyze(SourceProject project) => Analyze([project]);

    public IReadOnlyList<DesignDiagnostic> Analyze(IReadOnlyList<SourceProject> projects) =>
        projects
            .SelectMany(AnalyzeProject)
            .DistinctBy(CreateDiagnosticIdentity)
            .OrderBy(diagnostic => diagnostic.Location.FilePath, StringComparer.OrdinalIgnoreCase)
            .ThenBy(diagnostic => diagnostic.Location.Line)
            .ThenBy(diagnostic => diagnostic.Location.Column)
            .ThenBy(diagnostic => diagnostic.Rule.Id, StringComparer.Ordinal)
            .ToArray();

    private IEnumerable<DesignDiagnostic> AnalyzeProject(SourceProject project)
    {
        var context = new AnalysisContext(project);
        return _rules.SelectMany(rule => rule.Analyze(context));
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
