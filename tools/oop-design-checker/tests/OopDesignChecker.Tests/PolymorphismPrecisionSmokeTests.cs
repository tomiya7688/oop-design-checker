using OopDesignChecker.Core;
using OopDesignChecker.Rules;

namespace OopDesignChecker.Tests;

internal static class PolymorphismPrecisionSmokeTests
{
    public static void Run()
    {
        MatchingShapesWithoutSharedUsageAreAllowed();
        UnrelatedTypeBranchesAreAllowed();
        MarkerInterfaceIsNotUnnecessaryAbstraction();
        PublicSingleImplementationInterfaceIsExtensionPoint();
    }

    private static void MatchingShapesWithoutSharedUsageAreAllowed()
    {
        const string source = """
            internal sealed class CacheEntry
            {
                public void Open() { }
                public void Close() { }
            }

            internal sealed class Window
            {
                public void Open() { }
                public void Close() { }
            }

            internal sealed class Connection
            {
                public void Open() { }
                public void Close() { }
            }
            """;

        AssertNone(new MissingCommonAbstractionRule(), source, "OOP001");
    }

    private static void UnrelatedTypeBranchesAreAllowed()
    {
        const string source = """
            internal sealed class Command
            {
            }

            internal sealed class Snapshot
            {
            }

            internal sealed class Handler
            {
                public void Handle(object value)
                {
                    if (value is Command)
                    {
                    }
                    else if (value is Snapshot)
                    {
                    }
                }
            }
            """;

        AssertNone(new TypeBranchPolymorphismRule(), source, "OOP002");
    }

    private static void MarkerInterfaceIsNotUnnecessaryAbstraction()
    {
        const string source = """
            internal interface IMarker
            {
            }

            internal sealed class Tagged : IMarker
            {
            }
            """;

        AssertNone(new UnnecessaryAbstractionRule(), source, "OOP003");
    }

    private static void PublicSingleImplementationInterfaceIsExtensionPoint()
    {
        const string source = """
            public interface IPlugin
            {
                void Run();
            }

            internal sealed class BuiltInPlugin : IPlugin
            {
                public void Run() { }
            }
            """;

        AssertNone(new UnnecessaryAbstractionRule(), source, "OOP003");
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
