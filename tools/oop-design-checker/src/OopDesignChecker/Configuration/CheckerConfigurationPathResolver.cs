namespace OopDesignChecker.Configuration;

public static class CheckerConfigurationPathResolver
{
    public static string? Resolve(string targetPath, string? explicitPath)
    {
        var 対象ディレクトリ = 対象ディレクトリを取得する(targetPath);
        if (!string.IsNullOrWhiteSpace(explicitPath))
        {
            return ResolveExplicit(targetPath, explicitPath);
        }

        return 最も近い既定設定を探す(対象ディレクトリ);
    }

    public static string ResolveExplicit(string? targetPath, string explicitPath)
    {
        if (string.IsNullOrWhiteSpace(explicitPath))
        {
            throw new ArgumentException("Configuration path is required.", nameof(explicitPath));
        }

        if (Path.IsPathRooted(explicitPath))
        {
            var 絶対パス = Path.GetFullPath(explicitPath);
            設定ファイルの存在を確認する(絶対パス);
            return 絶対パス;
        }

        var 確認済みパス群 = new List<string>();
        var 作業ディレクトリ内パス = Path.GetFullPath(
            explicitPath,
            Directory.GetCurrentDirectory()
        );
        確認済みパス群.Add(作業ディレクトリ内パス);
        if (File.Exists(作業ディレクトリ内パス))
        {
            return 作業ディレクトリ内パス;
        }

        if (!string.IsNullOrWhiteSpace(targetPath))
        {
            var 対象ディレクトリ = 対象ディレクトリを取得する(targetPath);
            var 対象基準の相対パス = Path.GetFullPath(explicitPath, 対象ディレクトリ);
            if (!確認済みパス群.Contains(対象基準の相対パス, StringComparer.OrdinalIgnoreCase))
            {
                確認済みパス群.Add(対象基準の相対パス);
            }

            if (File.Exists(対象基準の相対パス))
            {
                return 対象基準の相対パス;
            }
        }

        throw new InvalidOperationException(
            $"Configuration file does not exist. Checked: {string.Join("; ", 確認済みパス群)}"
        );
    }

    private static string? 最も近い既定設定を探す(string 対象ディレクトリ)
    {
        var ディレクトリ = new DirectoryInfo(対象ディレクトリ);
        while (ディレクトリ is not null)
        {
            var 候補パス = Path.Combine(
                ディレクトリ.FullName,
                チェッカー設定読み込み器.DefaultFileName
            );
            if (File.Exists(候補パス))
            {
                return 候補パス;
            }

            ディレクトリ = ディレクトリ.Parent;
        }

        return null;
    }

    private static string 対象ディレクトリを取得する(string targetPath)
    {
        var 対象の絶対パス = Path.GetFullPath(targetPath);
        if (Directory.Exists(対象の絶対パス))
        {
            return 対象の絶対パス;
        }

        return Path.GetDirectoryName(対象の絶対パス) ?? Directory.GetCurrentDirectory();
    }

    private static void 設定ファイルの存在を確認する(string 設定ファイルパス)
    {
        if (!File.Exists(設定ファイルパス))
        {
            throw new InvalidOperationException(
                $"Configuration file does not exist: {設定ファイルパス}"
            );
        }
    }
}
