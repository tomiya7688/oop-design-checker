namespace OopDesignChecker.Analysis;

internal static class ProjectLoaderFactory
{
    public static IProjectLoader Create(IReadOnlyList<string> ignoredPaths) =>
        new CSharpProjectLoader(ignoredPaths);
}
