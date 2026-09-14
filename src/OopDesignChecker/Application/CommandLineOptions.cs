using OopDesignChecker.Core;

namespace OopDesignChecker.Application;

internal sealed record CommandLineOptions(
    string TargetPath,
    string? ConfigurationPath,
    DesignDiagnosticSeverity? FailureThreshold,
    bool Verbose);
