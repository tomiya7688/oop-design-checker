using System.Collections.Frozen;
using System.Text;
using System.Text.RegularExpressions;

namespace OopDesignChecker.Analysis;

internal static class PathFilter
{
    private static readonly FrozenSet<string> IgnoredDirectoryNames = new[]
    {
        "bin",
        "obj",
        ".git",
        ".vs",
    }.ToFrozenSet(StringComparer.OrdinalIgnoreCase);

    public static bool ShouldIgnore(string path) => HasBuiltInIgnoredSegment(path);

    public static bool ShouldIgnore(
        string path,
        string rootPath,
        IReadOnlyList<string> ignoredPatterns
    )
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
        return segments.Any(IgnoredDirectoryNames.Contains);
    }

    private static bool MatchesPattern(string relativePath, string pattern)
    {
        var normalizedPattern = pattern.Trim().Replace('\\', '/').TrimStart('/');
        if (normalizedPattern.Length == 0)
        {
            return false;
        }

        if (
            !normalizedPattern.Contains('/')
            && !normalizedPattern.Contains('*')
            && !normalizedPattern.Contains('?')
        )
        {
            return relativePath
                .Split('/', StringSplitOptions.RemoveEmptyEntries)
                .Contains(normalizedPattern, StringComparer.OrdinalIgnoreCase);
        }

        var regexPattern = BuildGlobRegex(normalizedPattern);
        return Regex.IsMatch(
            relativePath,
            regexPattern,
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant
        );
    }

    private static string BuildGlobRegex(string pattern)
    {
        var builder = new StringBuilder("^");

        for (var index = 0; index < pattern.Length; index++)
        {
            var current = pattern[index];
            if (current == '*' && index + 1 < pattern.Length && pattern[index + 1] == '*')
            {
                var followedBySlash = index + 2 < pattern.Length && pattern[index + 2] == '/';
                builder.Append(followedBySlash ? "(?:.*/)?" : ".*");
                index += followedBySlash ? 2 : 1;
                continue;
            }

            if (current == '*')
            {
                builder.Append("[^/]*");
                continue;
            }

            if (current == '?')
            {
                builder.Append("[^/]");
                continue;
            }

            builder.Append(Regex.Escape(current.ToString()));
        }

        builder.Append('$');
        return builder.ToString();
    }
}
