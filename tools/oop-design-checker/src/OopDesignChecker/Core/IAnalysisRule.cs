using OopDesignChecker.Analysis;

namespace OopDesignChecker.Core;

internal interface I解析規則
{
    RuleDescriptor 規則情報 { get; }

    IEnumerable<DesignDiagnostic> 解析する(AnalysisContext context);
}
