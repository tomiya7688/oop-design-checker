using OopDesignChecker.Analysis;

namespace OopDesignChecker.Core;

internal interface IAnalysisRule
{
    RuleDescriptor Descriptor { get; }

    IEnumerable<DesignDiagnostic> Analyze(AnalysisContext context);
}
