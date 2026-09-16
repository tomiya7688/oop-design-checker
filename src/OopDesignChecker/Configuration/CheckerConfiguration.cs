using OopDesignChecker.Core;

namespace OopDesignChecker.Configuration;

public sealed class CheckerConfiguration
{
    public IReadOnlyList<string> IgnoredPaths { get; init; } = [];

    public IReadOnlyList<string> DisabledRules { get; init; } = [];

    public DesignDiagnosticSeverity FailureThreshold { get; init; } =
        DesignDiagnosticSeverity.Danger;

    public CheckerRuleSettings RuleSettings { get; init; } = new();
}

public sealed class CheckerRuleSettings
{
    public InheritanceDepthRuleSettings Oop304 { get; init; } = new();
}

public sealed class InheritanceDepthRuleSettings
{
    public int WarningDepth { get; init; } = 4;
}
