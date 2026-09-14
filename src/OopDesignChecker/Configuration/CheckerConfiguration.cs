using OopDesignChecker.Core;

namespace OopDesignChecker.Configuration;

public sealed class CheckerConfiguration
{
    public string[] IgnoredPaths { get; init; } = [];

    public string[] DisabledRules { get; init; } = [];

    public DesignDiagnosticSeverity FailureThreshold { get; init; } = DesignDiagnosticSeverity.Danger;
}
