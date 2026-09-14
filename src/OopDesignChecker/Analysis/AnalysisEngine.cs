using OopDesignChecker.Core;

namespace OopDesignChecker.Analysis;

internal sealed class AnalysisEngine
{
    private readonly IReadOnlyList<IAnalysisRule> _rules;

    public AnalysisEngine(IReadOnlyList<IAnalysisRule> rules)
    {
        _rules = rules;
    }

    public IReadOnlyList<DesignDiagnostic> Analyze(SourceProject project)
    {
        var context = new AnalysisContext(project);

        return _rules
            .SelectMany(rule => rule.Analyze(context))
            .OrderBy(diagnostic => diagnostic.Location.FilePath, StringComparer.OrdinalIgnoreCase)
            .ThenBy(diagnostic => diagnostic.Location.Line)
            .ThenBy(diagnostic => diagnostic.Rule.Id, StringComparer.Ordinal)
            .ToArray();
    }
}
