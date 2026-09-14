namespace OopDesignChecker.Core;

internal sealed record RuleDescriptor(
    string Id,
    string Title,
    DesignDiagnosticSeverity DefaultSeverity);
