using OopDesignChecker.Core;
using OopDesignChecker.Localization;
using OopDesignChecker.Output;

namespace OopDesignChecker.Application;

internal sealed record コマンドライン引数(
    string TargetPath,
    string? ConfigurationPath,
    DesignDiagnosticSeverity? FailureThreshold,
    bool Verbose,
    診断出力形式 OutputFormat,
    string? OutputPath,
    UserInterfaceLanguage Language
)
{
    public コマンドライン引数(
        string targetPath,
        string? configurationPath,
        DesignDiagnosticSeverity? failureThreshold,
        bool verbose,
        診断出力形式 outputFormat,
        string? outputPath
    )
        : this(
            targetPath,
            configurationPath,
            failureThreshold,
            verbose,
            outputFormat,
            outputPath,
            UserInterfaceLanguage.Japanese
        ) { }
}
