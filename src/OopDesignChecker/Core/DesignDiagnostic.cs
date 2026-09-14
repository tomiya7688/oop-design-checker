namespace OopDesignChecker.Core;

public sealed record DesignDiagnostic(
    RuleDescriptor Rule,
    DesignDiagnosticSeverity Severity,
    string Message,
    string? SymbolName,
    SourceLocation Location);
