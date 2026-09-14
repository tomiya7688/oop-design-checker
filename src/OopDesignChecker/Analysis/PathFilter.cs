using System.Text.RegularExpressions;

namespace OopDesignChecker.Analysis;

internal static class PathFilter
{
    private static readonly string[] IgnoredDirectoryNames = ["bin", "obj", ".git", ".vs"];

    public static bool ShouldIgnore(string path) => HasBuiltInIgnoredSegment(path);

    public static bool ShouldIgnore(
        string path,
        string rootPath,
        IReadOnlyList<string> ignoredPatterns)
    {
        if (HasBuiltInIgnoredSegment(path))
        {
            return true;
        }

        var relativePath = Path.GetRelativePath(rootPath, path).Replace('\\', '/');
        return ignoredPatterns.Any(pattern => MatchesPattern(relativePath, pattern));
    }

    private static bool HasBuiltInIgnoredSegment(string path)
    {
        var segments = path.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        return segments.Any(segment => IgnoredDirectoryNames.Contains(segment, StringComparer.OrdinalIgnoreCase));
    }

    private static bool MatchesPattern(string relativePath, string pattern)
    {
        var normalizedPattern = pattern.Trim().Replace('\\', '/').TrimStart('/');
        if (normalizedPattern.Length == 0)
        {
            return false;
        }

        if (!normalizedPattern.Contains('/')
            && !normalizedPattern.Contains('*')
            && !normalizedPattern.Contains('?'))
        {
            return relativePath
                .Split('/', StringSplitOptions.RemoveEmptyEntries)
                .Contains(normalizedPattern, StringComparer.OrdinalIgnoreCase);
        }

        var regexPattern = "^" + Regex.Escape(normalizedPattern)
            .Replace(@"\*\*", ".*")
            .Replace(@"\*", "[^/]*")
            .Replace(@"\?", "[^/]") + "$";

        return Regex.IsMatch(relativePath, regexPattern, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    }
}
