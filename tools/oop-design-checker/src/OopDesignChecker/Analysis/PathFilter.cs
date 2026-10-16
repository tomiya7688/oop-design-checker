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

    public static bool 無視する(string 対象パス) => 組み込み除外区間を含む(対象パス);

    public static bool 無視する(
        string 対象パス,
        string 基準パス,
        IReadOnlyList<string> 除外パターン群
    )
    {
        if (組み込み除外区間を含む(対象パス))
        {
            return true;
        }

        var 相対パス = Path.GetRelativePath(基準パス, 対象パス).Replace('\\', '/');
        return 除外パターン群.Any(パターン => パターンに一致する(相対パス, パターン));
    }

    private static bool 組み込み除外区間を含む(string 対象パス)
    {
        var segments = 対象パス.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        return segments.Any(無視するディレクトリ名.Contains);
    }

    private static bool パターンに一致する(string 相対パス, string パターン)
    {
        var 正規化済みパターン = パターン.Trim().Replace('\\', '/').TrimStart('/');
        if (正規化済みパターン.Length == 0)
        {
            return false;
        }

        if (
            !正規化済みパターン.Contains('/')
            && !正規化済みパターン.Contains('*')
            && !正規化済みパターン.Contains('?')
        )
        {
            return 相対パス.Split('/', StringSplitOptions.RemoveEmptyEntries)
                .Contains(正規化済みパターン, StringComparer.OrdinalIgnoreCase);
        }

        var 正規表現パターン = ファイルパターン正規表現を作る(正規化済みパターン);
        return Regex.IsMatch(
            相対パス,
            正規表現パターン,
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant
        );
    }

    private static string ファイルパターン正規表現を作る(string パターン)
    {
        var 正規表現構築器 = new StringBuilder("^");

        for (var 位置 = 0; 位置 < パターン.Length; 位置++)
        {
            var 現在文字 = パターン[位置];
            if (現在文字 == '*' && 位置 + 1 < パターン.Length && パターン[位置 + 1] == '*')
            {
                var 後ろに区切りがある = 位置 + 2 < パターン.Length && パターン[位置 + 2] == '/';
                正規表現構築器.Append(後ろに区切りがある ? "(?:.*/)?" : ".*");
                位置 += 後ろに区切りがある ? 2 : 1;
                continue;
            }

            if (現在文字 == '*')
            {
                正規表現構築器.Append("[^/]*");
                continue;
            }

            if (現在文字 == '?')
            {
                正規表現構築器.Append("[^/]");
                continue;
            }

            正規表現構築器.Append(Regex.Escape(現在文字.ToString()));
        }

        正規表現構築器.Append('$');
        return 正規表現構築器.ToString();
    }
}
