using OopDesignChecker.Core;

namespace OopDesignChecker.Rules;

internal static class RuleCatalog
{
    public static IReadOnlyList<IAnalysisRule> CreateDefault() =>
        [
            new MissingCommonAbstractionRule(),
            new TypeBranchPolymorphismRule(),
            new ExcessiveVisibilityRule(),
            new SealingCandidateRule(),
            new StaticMemberCandidateRule(),
            new StaticClassCandidateRule(),
            new StatefulStaticDesignRule(),
            new EncapsulationLeakRule(),
            new OversizedMainOperationRule(),
            new ChildDisablesParentBehaviorRule(),
            new ExcessiveInheritanceDepthRule(),
            new ConcreteTypeDependencyRule(),
            new AvoidableConcreteConstructionRule(),
            new MultipleObjectsInClassRule(),
        ];
}
