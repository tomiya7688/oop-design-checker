using OopDesignChecker.Configuration;
using OopDesignChecker.Core;

namespace OopDesignChecker.Rules;

internal static class RuleCatalog
{
    public static IReadOnlyList<I解析規則> CreateDefault(CheckerConfiguration? configuration = null)
    {
        configuration ??= new CheckerConfiguration();

        return
        [
            new 共通抽象不足規則(),
            new 型分岐ポリモーフィズム規則(),
            new UnnecessaryAbstractionRule(),
            new 過剰可視性規則(),
            new 封止候補規則(),
            new StaticMemberCandidateRule(),
            new StaticClassCandidateRule(),
            new StatefulStaticDesignRule(),
            new EncapsulationLeakRule(),
            new ObjectInvariantBypassRule(),
            new ExcessiveExternalStateManipulationRule(),
            new OversizedMainOperationRule(),
            new 疑わしい継承関係規則(),
            new 親契約未使用規則(),
            new 子型による親動作無効化規則(),
            new 継承深度過剰規則(configuration.RuleSettings.Oop304.WarningDepth),
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
