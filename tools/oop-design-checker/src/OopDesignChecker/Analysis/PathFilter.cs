using System.Collections.Frozen;
using System.Text;
using System.Text.RegularExpressions;

namespace OopDesignChecker.Analysis;

internal static class パス除外判定器
{
    private static readonly FrozenSet<string> 無視するディレクトリ名 = new[]
    {
        "bin",
        "obj",
        ".git",
        ".vs",
    }.ToFrozenSet(StringComparer.OrdinalIgnoreCase);

    public static bool 無視する(string path) => 組み込み除外区間を含む(path);

    public static bool 無視する(string path, string rootPath, IReadOnlyList<string> ignoredPatterns)
    {
        if (組み込み除外区間を含む(path))
        {
            return true;
        }

        var relativePath = Path.GetRelativePath(rootPath, path).Replace('\\', '/');
        return ignoredPatterns.Any(pattern => パターンに一致する(relativePath, pattern));
    }

    private static bool 組み込み除外区間を含む(string path)
    {
        var segments = path.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        return segments.Any(無視するディレクトリ名.Contains);
    }

    private static bool パターンに一致する(string relativePath, string pattern)
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

        var regexPattern = glob正規表現を作る(normalizedPattern);
        return Regex.IsMatch(
            relativePath,
            regexPattern,
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant
        );
    }

    private static string glob正規表現を作る(string pattern)
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
