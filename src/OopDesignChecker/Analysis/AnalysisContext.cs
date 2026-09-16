namespace OopDesignChecker.Analysis;

internal sealed record AnalysisContext(SourceProject Project, IReadOnlyList<SourceProject> Projects)
{
    public AnalysisContext(SourceProject project)
        : this(project, [project]) { }
}
