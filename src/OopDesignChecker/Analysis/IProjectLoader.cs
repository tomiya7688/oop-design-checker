namespace OopDesignChecker.Analysis;

internal interface IProjectLoader
{
    IReadOnlyList<SourceProject> LoadProjects(string targetPath);
}
