namespace OopDesignChecker.Analysis;

internal static class PathFilter
{
    private static readonly string[] IgnoredDirectoryNames = ["bin", "obj", ".git", ".vs"];

    public static bool ShouldIgnore(string path)
    {
        var segments = path.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        return segments.Any(segment => IgnoredDirectoryNames.Contains(segment, StringComparer.OrdinalIgnoreCase));
    }
}
