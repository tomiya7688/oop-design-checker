using OopDesignChecker.Core;

namespace OopDesignChecker.Configuration;

public sealed class CheckerConfiguration
{
    public IReadOnlyList<string> IgnoredPaths { get; init; } = [];

    public IReadOnlyList<string> DisabledRules { get; init; } = [];

    public DesignDiagnosticSeverity FailureThreshold { get; init; } =
        DesignDiagnosticSeverity.Danger;
}
