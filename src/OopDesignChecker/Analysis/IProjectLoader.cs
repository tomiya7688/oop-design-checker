namespace OopDesignChecker.Analysis;

internal interface IProjectLoader
{
    SourceProject Load(string targetPath);
}
