using OopDesignChecker.Application;
using OopDesignChecker.Localization;

namespace OopDesignChecker.Output;

internal static class 診断書き込み器生成器
{
    public static 診断書き込み器 Create(
        診断出力形式 format,
        string? outputPath,
        UserInterfaceLanguage language = UserInterfaceLanguage.Japanese
    ) =>
        format switch
        {
            診断出力形式.Text => new コンソール診断書き込み器(outputPath, language),
            診断出力形式.Json => new JSON診断書き込み器(outputPath),
            診断出力形式.Sarif => new SARIF診断書き込み器(outputPath),
            診断出力形式.GitHub => new GitHub注釈診断書き込み器(),
            _ => throw new ArgumentOutOfRangeException(nameof(format), format, null),
        };
}
