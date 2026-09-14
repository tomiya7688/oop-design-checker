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
            new ObjectInvariantBypassRule(),
            new ExcessiveExternalStateManipulationRule(),
            new OversizedMainOperationRule(),
            new ParentContractMostlyUnusedRule(),
            new ChildDisablesParentBehaviorRule(),
            new ExcessiveInheritanceDepthRule(),
            new ConcreteTypeDependencyRule(),
            new AvoidableConcreteConstructionRule(),
            new CompositionCandidateRule(),
            new MultipleObjectsInClassRule(),
            new ExcessiveObjectNavigationRule(),
            new GetterSetterOnlyObjectRule(),
        ];
}
