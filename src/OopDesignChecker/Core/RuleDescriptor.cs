namespace OopDesignChecker.Core;

public sealed record RuleDescriptor(
    string Id,
    string Title,
    DesignDiagnosticSeverity DefaultSeverity);
