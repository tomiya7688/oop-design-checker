using OopDesignChecker.Core;

namespace OopDesignChecker.Output;

internal interface IDiagnosticWriter
{
    void Write(IReadOnlyList<DesignDiagnostic> diagnostics, bool verbose);
}
