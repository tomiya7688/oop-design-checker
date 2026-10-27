namespace OopDesignChecker.Configuration;

internal static class チェッカー設定読み込み器
{
    public const string DefaultFileName = "oop-design-checker.json";

    public static 読込済みチェッカー設定 Load(string targetPath, string? explicitPath)
    {
        var 設定ファイルパス = CheckerConfigurationPathResolver.Resolve(targetPath, explicitPath);
        if (設定ファイルパス is null)
        {
            return new 読込済みチェッカー設定(new CheckerConfiguration(), null);
        }

        try
        {
            var JSON本文 = File.ReadAllText(設定ファイルパス);
            var 設定 = CheckerConfigurationJson.Parse(JSON本文);
            return new 読込済みチェッカー設定(設定, 設定ファイルパス);
        }
        catch (Exception 例外)
            when (例外 is InvalidOperationException or IOException or UnauthorizedAccessException)
        {
            throw new 設定ファイル読込例外(設定ファイルパス, 例外);
        }
    }
}

/*
{
  "責務": "読み込みに失敗した設定ファイルのパスと原因を保持します。",
  "フィールド": {"設定ファイルパス": "実際に読み込みを試みた設定ファイルの絶対パスです。"}
}
*/
internal sealed class 設定ファイル読込例外(string 設定ファイルパス, Exception 原因)
    : InvalidOperationException(
        $"Invalid checker configuration '{設定ファイルパス}': {原因.Message}",
        原因
    )
{
    public string 設定ファイルパス { get; } = 設定ファイルパス;
}

internal sealed record 読込済みチェッカー設定(CheckerConfiguration Configuration, string? Path);
