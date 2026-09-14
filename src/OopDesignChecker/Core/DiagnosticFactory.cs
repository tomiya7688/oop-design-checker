using Microsoft.CodeAnalysis;

namespace OopDesignChecker.Core;

internal static class DiagnosticFactory
{
    public static DesignDiagnostic Create(
        RuleDescriptor descriptor,
        Location location,
        string message,
        string? symbolName = null,
        DesignDiagnosticSeverity? severity = null
    )
    {
        var lineSpan = location.GetLineSpan();
        var start = lineSpan.StartLinePosition;
        var filePath = string.IsNullOrWhiteSpace(lineSpan.Path) ? "<unknown>" : lineSpan.Path;

        return new DesignDiagnostic(
            descriptor,
            severity ?? descriptor.DefaultSeverity,
            message,
            symbolName,
            new SourceLocation(filePath, start.Line + 1, start.Character + 1)
        );
    }
}
