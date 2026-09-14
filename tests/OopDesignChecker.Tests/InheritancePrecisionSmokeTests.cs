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
        ValidSpecializationIsNotCompositionCandidate();
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
