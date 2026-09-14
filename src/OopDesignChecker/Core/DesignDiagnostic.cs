namespace OopDesignChecker.Core;

internal sealed record DesignDiagnostic(
    RuleDescriptor Rule,
    DesignDiagnosticSeverity Severity,
    string Message,
    string? SymbolName,
    SourceLocation Location);
