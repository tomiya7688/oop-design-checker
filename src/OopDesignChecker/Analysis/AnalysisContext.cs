namespace OopDesignChecker.Analysis;

internal sealed record AnalysisContext(
    SourceProject Project,
    IReadOnlyList<SourceProject> Projects,
    CancellationToken CancellationToken
)
{
    public AnalysisContext(SourceProject project)
        : this(project, [project], CancellationToken.None) { }

    public AnalysisContext(SourceProject project, IReadOnlyList<SourceProject> projects)
        : this(project, projects, CancellationToken.None) { }
}
