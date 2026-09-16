using OopDesignChecker.Configuration;
using OopDesignChecker.Core;

namespace OopDesignChecker.Rules;

internal static class RuleCatalog
{
    public static IReadOnlyList<IAnalysisRule> CreateDefault(
        CheckerConfiguration? configuration = null
    )
    {
        configuration ??= new CheckerConfiguration();

        return
        [
            new MissingCommonAbstractionRule(),
            new TypeBranchPolymorphismRule(),
            new UnnecessaryAbstractionRule(),
            new ExcessiveVisibilityRule(),
            new SealingCandidateRule(),
            new StaticMemberCandidateRule(),
            new StaticClassCandidateRule(),
            new StatefulStaticDesignRule(),
            new EncapsulationLeakRule(),
            new ObjectInvariantBypassRule(),
            new ExcessiveExternalStateManipulationRule(),
            new OversizedMainOperationRule(),
            new SuspiciousInheritanceRelationshipRule(),
            new ParentContractMostlyUnusedRule(),
            new ChildDisablesParentBehaviorRule(),
            new ExcessiveInheritanceDepthRule(configuration.RuleSettings.Oop304.WarningDepth),
            new ConcreteTypeDependencyRule(),
            new AvoidableConcreteConstructionRule(),
            new CompositionCandidateRule(),
            new MultipleObjectsInClassRule(),
            new AnemicObjectRule(),
            new ExcessiveUnrelatedDependenciesRule(),
            new ExcessiveObjectNavigationRule(),
            new GetterSetterOnlyObjectRule(),
        ];
    }
}
