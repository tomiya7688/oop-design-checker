using OopDesignChecker.Core;
using OopDesignChecker.Rules;

namespace OopDesignChecker.Tests;

internal static class InheritancePrecisionSmokeTests
{
    public static void Run()
    {
        MarkerInterfaceDoesNotCreateConcreteDependencyWarning();
        MarkerInterfaceDoesNotCreateConstructionWarning();
        OwnedFactoryConstructionIsAllowed();
        ProjectSurfaceReplacementIsSuspicious();
        ExternalBaseSurfaceReplacementIsAllowed();
        ImplementationReuseInheritanceIsAttention();
        ConstructorOnlyProtectedUseIsAllowed();
        ValidSpecializationIsNotCompositionCandidate();
        ParentContractPrecisionSmokeTests.Run();
    }

    private static void MarkerInterfaceDoesNotCreateConcreteDependencyWarning()
    {
        const string source = """
            internal interface IMarker
            {
            }

            internal sealed class TaggedService : IMarker
            {
                public void Run() { }
            }

            internal sealed class Consumer
            {
                public Consumer(TaggedService service) { }
            }
            """;

        AssertNone(new ConcreteTypeDependencyRule(), source, "OOP305");
    }

    private static void MarkerInterfaceDoesNotCreateConstructionWarning()
    {
        const string source = """
            internal interface IMarker
            {
            }

            internal sealed class TaggedService : IMarker
            {
                public void Run() { }
            }

            internal sealed class Consumer
            {
                private readonly TaggedService _service = new();
            }
            """;

        AssertNone(new AvoidableConcreteConstructionRule(), source, "OOP306");
    }

    private static void OwnedFactoryConstructionIsAllowed()
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

            internal sealed class BackupClock : IClock
            {
                public int Read() => 1;
            }

            internal sealed class ClockFactory
            {
                public IClock Create() => new Clock();
            }
            """;

        AssertNone(new AvoidableConcreteConstructionRule(), source, "OOP306");
    }

    private static void ProjectSurfaceReplacementIsSuspicious()
    {
        const string source = """
            internal class Device
            {
                public void Start() { }
                public int Status => 0;
            }

            internal sealed class SpecializedDevice : Device
            {
                public new void Start() { }
                public new int Status => 1;
            }
            """;

        AssertSingle(
            new SuspiciousInheritanceRelationshipRule(),
            source,
            "OOP301",
            DesignDiagnosticSeverity.Warning
        );
    }

    private static void ExternalBaseSurfaceReplacementIsAllowed()
    {
        const string source = """
            internal sealed class CustomException : System.Exception
            {
                public new string Message => "custom";
                public new string Source
                {
                    get => "custom";
                    set { }
                }
            }
            """;

        AssertNone(new SuspiciousInheritanceRelationshipRule(), source, "OOP301");
    }

    private static void ImplementationReuseInheritanceIsAttention()
    {
        const string source = """
            internal class CalculatorBase
            {
                protected int ReadLeft() => 1;
                protected int ReadRight() => 2;
            }

            internal sealed class TotalCalculator : CalculatorBase
            {
                public int Calculate() => ReadLeft() + ReadRight();
            }
            """;

        AssertSingle(
            new CompositionCandidateRule(),
            source,
            "OOP307",
            DesignDiagnosticSeverity.Attention
        );
    }

    private static void ConstructorOnlyProtectedUseIsAllowed()
    {
        const string source = """
            internal class ConfigurationBase
            {
                protected int Left;
                protected int Right;
            }

            internal sealed class Configuration : ConfigurationBase
            {
                public Configuration(int left, int right)
                {
                    Left = left;
                    Right = right;
                }
            }
            """;

        AssertNone(new CompositionCandidateRule(), source, "OOP307");
    }

    private static void ValidSpecializationIsNotCompositionCandidate()
    {
        const string source = """
            internal class Processor
            {
                protected int ReadLeft() => 1;
                protected int ReadRight() => 2;
                public virtual int Run() => 0;
            }

            internal sealed class SpecializedProcessor : Processor
            {
                public override int Run() => ReadLeft() + ReadRight();
            }
            """;

        AssertNone(new CompositionCandidateRule(), source, "OOP307");
    }

    private static void AssertSingle(
        IAnalysisRule rule,
        string source,
        string ruleId,
        DesignDiagnosticSeverity severity
    )
    {
        var diagnostics = rule.Analyze(TestProjectFactory.Create(source)).ToArray();
        if (
            diagnostics.Length == 1
            && diagnostics[0].Rule.Id == ruleId
            && diagnostics[0].Severity == severity
        )
        {
            return;
        }

        var found = string.Join(
            ", ",
            diagnostics.Select(item => $"{item.Rule.Id}:{item.Severity}")
        );
        throw new InvalidOperationException(
            $"Expected one {ruleId}:{severity} diagnostic, found [{found}]."
        );
    }

    private static void AssertNone(IAnalysisRule rule, string source, string ruleId)
    {
        var diagnostics = rule.Analyze(TestProjectFactory.Create(source)).ToArray();
        if (diagnostics.Length == 0)
        {
            return;
        }

        var found = string.Join(", ", diagnostics.Select(item => item.Rule.Id));
        throw new InvalidOperationException($"Expected no {ruleId} diagnostic, found [{found}].");
    }
}
