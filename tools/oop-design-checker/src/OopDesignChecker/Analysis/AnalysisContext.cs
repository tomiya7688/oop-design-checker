namespace OopDesignChecker.Analysis;

internal sealed record AnalysisContext(
    解析対象プロジェクト Project,
    IReadOnlyList<解析対象プロジェクト> Projects,
    CancellationToken CancellationToken
)
{
    public AnalysisContext(解析対象プロジェクト project)
        : this(project, [project], CancellationToken.None) { }

    public AnalysisContext(
        解析対象プロジェクト project,
        IReadOnlyList<解析対象プロジェクト> projects
    )
        : this(project, projects, CancellationToken.None) { }
}
