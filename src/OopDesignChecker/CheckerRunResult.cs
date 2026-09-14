using OopDesignChecker.Core;

namespace OopDesignChecker;

public sealed record CheckerRunResult(
    IReadOnlyList<DesignDiagnostic> Diagnostics,
    DesignDiagnosticSeverity FailureThreshold,
    string? ConfigurationPath)
{
    public bool ShouldFail => Diagnostics.Any(diagnostic => diagnostic.Severity >= FailureThreshold);
}
