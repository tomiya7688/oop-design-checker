using OopDesignChecker.Core;

namespace OopDesignChecker.Output;

internal interface 診断書き込み器
{
    void Write(IReadOnlyList<DesignDiagnostic> diagnostics, bool verbose);
}
