using OopDesignChecker.Core;

namespace OopDesignChecker.Localization;

public static class LocalizedText
{
    public static UserInterfaceLanguage DefaultLanguage => UserInterfaceLanguage.Japanese;

    public static bool TryParseLanguage(string? value, out UserInterfaceLanguage language)
    {
        switch (value?.Trim().ToLowerInvariant())
        {
            case "ja":
            case "jp":
            case "japanese":
                language = UserInterfaceLanguage.Japanese;
                return true;
            case "en":
            case "english":
                language = UserInterfaceLanguage.English;
                return true;
            default:
                language = default;
                return false;
        }
    }

    public static string LanguageCode(UserInterfaceLanguage language) =>
        language switch
        {
            UserInterfaceLanguage.Japanese => "ja",
            UserInterfaceLanguage.English => "en",
            _ => throw new ArgumentOutOfRangeException(nameof(language), language, null),
        };

    public static string Usage(UserInterfaceLanguage language) =>
        language switch
        {
            UserInterfaceLanguage.Japanese =>
                "使用方法: oop-design-checker [path] [--config file] [--fail-on danger|warning|attention] [--format text|json|sarif|github] [--output file] [--language ja|en] [--verbose] [--version]",
            UserInterfaceLanguage.English =>
                "Usage: oop-design-checker [path] [--config file] [--fail-on danger|warning|attention] [--format text|json|sarif|github] [--output file] [--language ja|en] [--verbose] [--version]",
            _ => throw new ArgumentOutOfRangeException(nameof(language), language, null),
        };

    public static string ConfigRequiresPath(UserInterfaceLanguage language) =>
        Select(
            language,
            "--config には設定ファイルのpathが必要です。",
            "--config requires a file path."
        );

    public static string FailOnRequiresSeverity(UserInterfaceLanguage language) =>
        Select(
            language,
            "--fail-on には danger、warning、attention のいずれかを指定してください。",
            "--fail-on requires danger, warning, or attention."
        );

    public static string FormatRequiresValue(UserInterfaceLanguage language) =>
        Select(
            language,
            "--format には text、json、sarif、github のいずれかを指定してください。",
            "--format requires text, json, sarif, or github."
        );

    public static string OutputRequiresPath(UserInterfaceLanguage language) =>
        Select(
            language,
            "--output には出力ファイルのpathが必要です。",
            "--output requires a file path."
        );

    public static string LanguageRequiresValue(UserInterfaceLanguage language) =>
        Select(
            language,
            "--language には ja または en を指定してください。",
            "--language requires ja or en."
        );

    public static string GitHubOutputCannotUseFile(UserInterfaceLanguage language) =>
        Select(
            language,
            "--format github はworkflow annotationをstdoutへ出力するため、--outputは併用できません。",
            "--output cannot be used with --format github because workflow annotations must be written to stdout."
        );

    public static string UnknownOption(UserInterfaceLanguage language, string option) =>
        Select(language, $"不明なoptionです: {option}", $"Unknown option: {option}");

    public static string OnlyOneTarget(UserInterfaceLanguage language) =>
        Select(
            language,
            "target pathは1つだけ指定できます。",
            "Only one target path can be specified."
        );

    public static string AnalysisFailed(UserInterfaceLanguage language, string message) =>
        Select(language, $"解析に失敗しました: {message}", $"Analysis failed: {message}");

    public static string LoadedConfig(UserInterfaceLanguage language, string path) =>
        Select(language, $"設定: {path}", $"Config: {path}");

    public static string DiagnosticsSummary(
        UserInterfaceLanguage language,
        int dangerCount,
        int warningCount,
        int attentionCount
    ) =>
        Select(
            language,
            $"診断: 危険 {dangerCount}件、警告 {warningCount}件、注意 {attentionCount}件。",
            $"Diagnostics: {dangerCount} danger, {warningCount} warning(s), {attentionCount} attention."
        );

    public static string SeverityName(
        UserInterfaceLanguage language,
        DesignDiagnosticSeverity severity
    ) =>
        (language, severity) switch
        {
            (UserInterfaceLanguage.Japanese, DesignDiagnosticSeverity.Attention) => "注意",
            (UserInterfaceLanguage.Japanese, DesignDiagnosticSeverity.Warning) => "警告",
            (UserInterfaceLanguage.Japanese, DesignDiagnosticSeverity.Danger) => "危険",
            (UserInterfaceLanguage.English, DesignDiagnosticSeverity.Attention) => "Attention",
            (UserInterfaceLanguage.English, DesignDiagnosticSeverity.Warning) => "Warning",
            (UserInterfaceLanguage.English, DesignDiagnosticSeverity.Danger) => "Danger",
            _ => throw new ArgumentOutOfRangeException(nameof(severity), severity, null),
        };

    private static string Select(
        UserInterfaceLanguage language,
        string japanese,
        string english
    ) =>
        language switch
        {
            UserInterfaceLanguage.Japanese => japanese,
            UserInterfaceLanguage.English => english,
            _ => throw new ArgumentOutOfRangeException(nameof(language), language, null),
        };
}
