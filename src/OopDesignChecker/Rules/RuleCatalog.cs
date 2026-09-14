using OopDesignChecker.Core;

namespace OopDesignChecker.Rules;

internal static class RuleCatalog
{
    public static IReadOnlyList<IAnalysisRule> CreateDefault() =>
    [
        new TypeBranchPolymorphismRule(),
        new StaticMemberCandidateRule(),
        new StaticClassCandidateRule(),
        new EncapsulationLeakRule(),
        new OversizedMainOperationRule(),
        new ChildDisablesParentBehaviorRule(),
        new ExcessiveInheritanceDepthRule()
    ];
}
