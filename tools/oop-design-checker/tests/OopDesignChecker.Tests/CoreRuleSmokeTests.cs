using OopDesignChecker.Core;
using OopDesignChecker.Rules;

namespace OopDesignChecker.Tests;

internal static class CoreRuleSmokeTests
{
    public static void Run()
    {
        MissingCommonAbstractionIsDetected();
        SealingCandidateIsAttention();
        StatefulStaticDesignIsWarning();
        ConcreteDependencyDespiteAbstractionIsWarning();
        AvoidableConcreteConstructionIsWarning();
        独立オブジェクトまとまりを警告する();
        プロパティ状態まとまりを警告する();
        補助処理で連結した状態は警告しない();
        InheritancePrecisionSmokeTests.Run();
        InheritanceDepthPrecisionSmokeTests.Run();
        PolymorphismPrecisionSmokeTests.Run();
        EncapsulationPrecisionSmokeTests.Run();
        StaticPrecisionSmokeTests.Run();
        OperationPrecisionSmokeTests.Run();
        DiagnosticCoordinationSmokeTests.Run();
        ObjectNavigationRuleTests.Run();
        ExtendedRuleSmokeTests.Run();
    }

    private static void MissingCommonAbstractionIsDetected()
    {
        const string source = """
            internal sealed class Dog
            {
                public void Move() { }
                public void Eat() { }
            }

            internal sealed class Cat
            {
                public void Move() { }
                public void Eat() { }
            }

            internal sealed class Bird
            {
                public void Move() { }
                public void Eat() { }
            }

            internal sealed class Zoo
            {
                public void Handle(Dog animal) { }
                public void Handle(Cat animal) { }
                public void Handle(Bird animal) { }
            }
            """;

        AssertSingle(new 共通抽象不足規則(), source, "OOP001", DesignDiagnosticSeverity.Warning);
    }

    private static void SealingCandidateIsAttention()
    {
        const string source = """
            internal class Worker
            {
                private int _count;
                public void Run() => _count++;
            }
            """;

        AssertSingle(new 封止候補規則(), source, "OOP102", DesignDiagnosticSeverity.Attention);
    }

    private static void StatefulStaticDesignIsWarning()
    {
        const string source = """
            internal static class GlobalState
            {
                private static int _count;
                public static void Increment() => _count++;
            }
            """;

        AssertSingle(
            new StatefulStaticDesignRule(),
            source,
            "OOP105",
            DesignDiagnosticSeverity.Warning
        );
    }

    private static void ConcreteDependencyDespiteAbstractionIsWarning()
    {
        const string source = """
            internal interface IClock
            {
                int Read();
            }

            internal sealed class Clock : IClock
            {
                public int Read() => 0;
            }

            internal sealed class Service
            {
                private readonly Clock _clock;
                public Service(Clock clock) => _clock = clock;
                public int Run() => _clock.Read();
            }
            """;

        AssertSingle(
            new ConcreteTypeDependencyRule(),
            source,
            "OOP305",
            DesignDiagnosticSeverity.Warning
        );
    }

    private static void AvoidableConcreteConstructionIsWarning()
    {
        const string source = """
            internal interface IClock
            {
                int Read();
            }

            internal sealed class Clock : IClock
            {
                public int Read() => 0;
            }

            internal sealed class Service
            {
                private readonly IClock _clock = new Clock();
                public int Run() => _clock.Read();
            }
            """;

        AssertSingle(
            new AvoidableConcreteConstructionRule(),
            source,
            "OOP306",
            DesignDiagnosticSeverity.Warning
        );
    }

    private static void 独立オブジェクトまとまりを警告する()
    {
        const string source = """
            internal sealed class Combined
            {
                private int _leftA;
                private int _leftB;
                private int _leftC;
                private int _rightA;
                private int _rightB;
                private int _rightC;

                public void LeftOne() => _leftA = _leftB + 1;
                public void LeftTwo() => _leftB = _leftC + 1;
                public void LeftThree() => _leftC = _leftA + 1;

                public void RightOne() => _rightA = _rightB + 1;
                public void RightTwo() => _rightB = _rightC + 1;
                public void RightThree() => _rightC = _rightA + 1;
            }
            """;

        AssertSingle(
            new 複数オブジェクト混在規則(),
            source,
            "OOP401",
            DesignDiagnosticSeverity.Warning
        );
    }

    private static void プロパティ状態まとまりを警告する()
    {
        const string source = """
            internal sealed class Combined
            {
                private int LeftA { get; set; }
                private int LeftB { get; set; }
                private int LeftC { get; set; }
                private int RightA { get; set; }
                private int RightB { get; set; }
                private int RightC { get; set; }

                public void LeftOne() => LeftA = LeftB + 1;
                public void LeftTwo() => LeftB = LeftC + 1;
                public void LeftThree() => LeftC = LeftA + 1;

                public void RightOne() => RightA = RightB + 1;
                public void RightTwo() => RightB = RightC + 1;
                public void RightThree() => RightC = RightA + 1;
            }
            """;

        AssertSingle(
            new 複数オブジェクト混在規則(),
            source,
            "OOP401",
            DesignDiagnosticSeverity.Warning
        );
    }

    private static void 補助処理で連結した状態は警告しない()
    {
        const string source = """
            internal sealed class CoherentObject
            {
                private int _leftA;
                private int _leftB;
                private int _leftC;
                private int _rightA;
                private int _rightB;
                private int _rightC;

                public void Run()
                {
                    LeftOne();
                    RightOne();
                }

                private void LeftOne() => _leftA = _leftB + 1;
                private void LeftTwo() => _leftB = _leftC + 1;
                private void LeftThree() => _leftC = _leftA + 1;

                private void RightOne() => _rightA = _rightB + 1;
                private void RightTwo() => _rightB = _rightC + 1;
                private void RightThree() => _rightC = _rightA + 1;
            }
            """;

        AssertNone(new 複数オブジェクト混在規則(), source, "OOP401");
    }

    private static void AssertSingle(
        I解析規則 rule,
        string source,
        string ruleId,
        DesignDiagnosticSeverity severity
    )
    {
        var diagnostics = rule.解析する(TestProjectFactory.解析文脈を作る(source)).ToArray();
        if (
            diagnostics.Length != 1
            || diagnostics[0].Rule.Id != ruleId
            || diagnostics[0].Severity != severity
        )
        {
            var found = string.Join(
                ", ",
                diagnostics.Select(item => $"{item.Rule.Id}:{item.Severity}")
            );
            throw new InvalidOperationException(
                $"Expected one {ruleId}:{severity} diagnostic, found [{found}]."
            );
        }
    }

    private static void AssertNone(複数オブジェクト混在規則 rule, string source, string ruleId)
    {
        var diagnostics = rule.解析する(TestProjectFactory.解析文脈を作る(source)).ToArray();
        if (diagnostics.Length == 0)
        {
            return;
        }

        var found = string.Join(", ", diagnostics.Select(item => item.Rule.Id));
        throw new InvalidOperationException($"Expected no {ruleId} diagnostic, found [{found}].");
    }
}
