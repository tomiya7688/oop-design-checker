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
        catch (InvalidOperationException 例外)
        {
            throw new InvalidOperationException(
                $"Invalid checker configuration '{設定ファイルパス}': {例外.Message}",
                例外
            );
        }
    }
}

internal sealed record 読込済みチェッカー設定(CheckerConfiguration Configuration, string? Path);
