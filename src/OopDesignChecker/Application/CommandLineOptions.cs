using OopDesignChecker.Core;
using OopDesignChecker.Localization;

namespace OopDesignChecker.Application;

internal sealed record CommandLineOptions(
    string TargetPath,
    string? ConfigurationPath,
    DesignDiagnosticSeverity? FailureThreshold,
    bool Verbose,
    DiagnosticOutputFormat OutputFormat,
    string? OutputPath,
    UserInterfaceLanguage Language
)
{
    public CommandLineOptions(
        string targetPath,
        string? configurationPath,
        DesignDiagnosticSeverity? failureThreshold,
        bool verbose,
        DiagnosticOutputFormat outputFormat,
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
