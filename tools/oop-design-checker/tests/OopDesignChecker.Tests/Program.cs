using Microsoft.CodeAnalysis;
using OopDesignChecker.Analysis;
using OopDesignChecker.Core;
using OopDesignChecker.Rules;

namespace OopDesignChecker.Tests;

internal static class Program
{
    private static readonly IReadOnlyList<TestCase> TestCases =
    [
        new(
            "project loader resolves real project/package references",
            ProjectLoaderResolvesProjectReferences
        ),
        new(
            "project loader rejects broken loose-source compilations",
            ProjectLoaderRejectsBrokenCompilation
        ),
        new(
            "project loader keeps multi-project compilations separate",
            ProjectLoaderKeepsMultiProjectCompilationsSeparate
        ),
        new(
            "checker service analyzes SDK-style project folders",
            CheckerServiceAnalyzesSdkProjectFolder
        ),
        new(
            "project loader analyzes solution root folders",
            ProjectLoaderAnalyzesSolutionRootFolder
        ),
        new(
            "diagnostic identity preserves platform path case semantics",
            DiagnosticIdentityPreservesPlatformPathCase
        ),
        new(
            "OOP101 detects public classes confined to an inheritance hierarchy",
            ExcessiveVisibilityIsDetected
        ),
        new("OOP106 detects public mutable fields as danger", EncapsulationLeakIsDetected),
        new(
            "OOP106 detects mutable collection exposure as danger",
            MutableCollectionExposureIsDetected
        ),
        new(
            "OOP106 detects unnecessarily public setters as warning",
            UnnecessaryPublicSetterIsDetected
        ),
        new("OOP106 keeps externally used setters public", ExternallyUsedSetterIsAllowed),
        new("OOP103 static member candidates are attention", StaticMemberCandidateIsDetected),
        new(
            "OOP103 does not suggest static when inherited instance state is used",
            InheritedInstanceStateIsNotStaticCandidate
        ),
        new("OOP104 static class candidates are attention", StaticClassCandidateIsDetected),
        new("OOP002 type branching is warning", TypeBranchingIsDetected),
        new("OOP303 disabled parent behavior is danger", DisabledParentBehaviorIsDetected),
        new("OOP304 deep inheritance is attention", DeepInheritanceIsDetected),
        new("core OOP rule sprint", CoreRuleSmokeTests.Run),
        new("CI diagnostic output formats", OutputFormatSmokeTests.Run),
        new("Japanese and English UI localization", LocalizationSmokeTests.Run),
        new(
            "GUI configuration, export, and cancellation workflow",
            GuiWorkflowPhase2SmokeTests.Run
        ),
        new(
            "configuration disables selected rules",
            ConfigurationBehaviorTests.DisabledRulesAreSuppressed
        ),
        new(
            "configuration ignores matching paths",
            ConfigurationBehaviorTests.IgnoredPathsAreExcluded
        ),
        new(
            "configuration controls failure threshold",
            ConfigurationBehaviorTests.FailureThresholdIsLoaded
        ),
    ];

    private static int Main()
    {
        var failed = 0;

        foreach (var testCase in TestCases)
        {
            try
            {
                testCase.Run();
                Console.WriteLine($"PASS {testCase.Name}");
            }
            catch (Exception exception)
            {
                failed++;
                Console.Error.WriteLine($"FAIL {testCase.Name}");
                Console.Error.WriteLine(exception.Message);
            }
        }

        Console.WriteLine($"{TestCases.Count - failed}/{TestCases.Count} tests passed.");
        return failed == 0 ? 0 : 1;
    }

    private static void ProjectLoaderResolvesProjectReferences()
    {
        var repositoryRoot = FindRepositoryRoot();
        var projectPath = Path.Combine(
            repositoryRoot,
            "src",
            "OopDesignChecker",
            "OopDesignChecker.csproj"
        );

        var project = new CSharpProjectLoader().Load(projectPath);
        var errors = project
            .Compilation.GetDiagnostics()
            .Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error)
            .ToArray();

        if (errors.Length > 0)
        {
            throw new InvalidOperationException(
                "Expected project compilation without errors:\n"
                    + string.Join("\n", errors.Select(error => error.ToString()))
            );
        }

        if (project.SyntaxTrees.Any(tree => PathFilter.ShouldIgnore(tree.FilePath)))
        {
            throw new InvalidOperationException(
                "Generated bin/obj source leaked into rule traversal."
            );
        }
    }

    private static void ProjectLoaderKeepsMultiProjectCompilationsSeparate()
    {
        var repositoryRoot = FindRepositoryRoot();
        var temporarySolution = Path.Combine(
            repositoryRoot,
            $"oop-checker-multi-project-{Guid.NewGuid():N}.slnx"
        );

        File.WriteAllText(
            temporarySolution,
            """
            <Solution>
              <Project Path="src/OopDesignChecker/OopDesignChecker.csproj" />
              <Project Path="src/frontends/cui/OopDesignChecker.Cui.csproj" />
            </Solution>
            """
        );

        try
        {
            var projects = new CSharpProjectLoader().LoadProjects(temporarySolution);
            if (projects.Count != 2)
            {
                throw new InvalidOperationException(
                    $"Expected 2 separately loaded projects, found {projects.Count}."
                );
            }

            var assemblyNames = projects
                .Select(project => project.Compilation.AssemblyName)
                .Where(name => !string.IsNullOrWhiteSpace(name))
                .Distinct(StringComparer.Ordinal)
                .ToArray();
            if (assemblyNames.Length != 2)
            {
                throw new InvalidOperationException(
                    "Expected each project to retain its own compilation."
                );
            }
        }
        finally
        {
            File.Delete(temporarySolution);
        }
    }

    private static void CheckerServiceAnalyzesSdkProjectFolder()
    {
        var temporaryDirectory = Path.Combine(
            Path.GetTempPath(),
            $"oop-checker-folder-project-{Guid.NewGuid():N}"
        );
        Directory.CreateDirectory(temporaryDirectory);

        try
        {
            File.WriteAllText(
                Path.Combine(temporaryDirectory, "FolderTarget.csproj"),
                """
                <Project Sdk="Microsoft.NET.Sdk">
                  <PropertyGroup>
                    <TargetFramework>net10.0</TargetFramework>
                  </PropertyGroup>
                </Project>
                """
            );
            File.WriteAllText(
                Path.Combine(temporaryDirectory, "Sample.cs"),
                "internal sealed class Sample { private int _value; public int Value => _value; }"
            );

            _ = CheckerService.Analyze(temporaryDirectory);
        }
        finally
        {
            Directory.Delete(temporaryDirectory, recursive: true);
        }
    }

    private static void ProjectLoaderAnalyzesSolutionRootFolder()
    {
        var repositoryRoot = FindRepositoryRoot();
        var projects = new CSharpProjectLoader().LoadProjects(repositoryRoot);
        if (projects.Count < 1)
        {
            throw new InvalidOperationException(
                "Expected the solution root folder to load at least one C# project."
            );
        }
    }

    private static void DiagnosticIdentityPreservesPlatformPathCase()
    {
        var root = Path.Combine(Path.GetTempPath(), $"oop-checker-case-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);

        try
        {
            var upper = CreateProjectForPath(Path.Combine(root, "Foo.cs"));
            var lower = CreateProjectForPath(Path.Combine(root, "foo.cs"));
            var diagnostics = new AnalysisEngine([new EncapsulationLeakRule()])
                .Analyze([upper, lower])
                .Where(diagnostic => diagnostic.Rule.Id == "OOP106")
                .ToArray();

            var expected = OperatingSystem.IsWindows() ? 1 : 2;
            if (diagnostics.Length != expected)
            {
                throw new InvalidOperationException(
                    $"Expected {expected} case-distinct diagnostic(s), found {diagnostics.Length}."
                );
            }
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    private static SourceProject CreateProjectForPath(string filePath)
    {
        const string source = "internal sealed class Same { public int Value; }";
        var syntaxTree = Microsoft.CodeAnalysis.CSharp.CSharpSyntaxTree.ParseText(
            source,
            path: filePath
        );
        var compilation = Microsoft.CodeAnalysis.CSharp.CSharpCompilation.Create(
            Path.GetFileNameWithoutExtension(filePath),
            [syntaxTree],
            MetadataReferenceProvider.CreatePlatformReferences(),
            new Microsoft.CodeAnalysis.CSharp.CSharpCompilationOptions(
                Microsoft.CodeAnalysis.OutputKind.DynamicallyLinkedLibrary
            )
        );
        var semanticModels = new Dictionary<
            Microsoft.CodeAnalysis.SyntaxTree,
            Microsoft.CodeAnalysis.SemanticModel
        >
        {
            [syntaxTree] = compilation.GetSemanticModel(syntaxTree),
        };

        return new SourceProject(
            Path.GetDirectoryName(filePath)!,
            isApplication: false,
            compilation,
            semanticModels,
            [syntaxTree]
        );
    }

    private static void ProjectLoaderRejectsBrokenCompilation()
    {
        var temporaryDirectory = Path.Combine(
            Path.GetTempPath(),
            $"oop-checker-test-{Guid.NewGuid():N}"
        );
        Directory.CreateDirectory(temporaryDirectory);

        try
        {
            var sourcePath = Path.Combine(temporaryDirectory, "Broken.cs");
            File.WriteAllText(sourcePath, "internal sealed class Broken { MissingType Value; }");

            try
            {
                _ = new CSharpProjectLoader().Load(sourcePath);
            }
            catch (InvalidOperationException exception)
                when (exception.Message.Contains(
                        "could not be analyzed reliably",
                        StringComparison.Ordinal
                    )
                )
            {
                return;
            }

            throw new InvalidOperationException(
                "Broken source was accepted as a reliable compilation."
            );
        }
        finally
        {
            Directory.Delete(temporaryDirectory, recursive: true);
        }
    }

    private static void ExcessiveVisibilityIsDetected()
    {
        const string source = """
            public class Base
            {
            }

            internal sealed class Child : Base
            {
            }
            """;

        var diagnostics = Run(new ExcessiveVisibilityRule(), source);
        AssertSingleRule(diagnostics, "OOP101", DesignDiagnosticSeverity.Warning);
    }

    private static void EncapsulationLeakIsDetected()
    {
        const string source = """
            internal sealed class Sample
            {
                public int Value;
            }
            """;

        var diagnostics = Run(new EncapsulationLeakRule(), source);
        AssertSingleRule(diagnostics, "OOP106", DesignDiagnosticSeverity.Danger);
    }

    private static void MutableCollectionExposureIsDetected()
    {
        const string source = """
            using System.Collections.Generic;

            internal sealed class Bag
            {
                private readonly List<int> _items = new();

                public List<int> Items => _items;
            }
            """;

        var diagnostics = Run(new EncapsulationLeakRule(), source);
        AssertSingleRule(diagnostics, "OOP106", DesignDiagnosticSeverity.Danger);
    }

    private static void UnnecessaryPublicSetterIsDetected()
    {
        const string source = """
            internal sealed class Counter
            {
                public int Value { get; set; }

                public void Increment()
                {
                    Value++;
                }
            }
            """;

        var diagnostics = Run(new EncapsulationLeakRule(), source);
        AssertSingleRule(diagnostics, "OOP106", DesignDiagnosticSeverity.Warning);
    }

    private static void ExternallyUsedSetterIsAllowed()
    {
        const string source = """
            internal sealed class Counter
            {
                public int Value { get; set; }

                public void Increment()
                {
                    Value++;
                }
            }

            internal sealed class CounterEditor
            {
                public void Reset(Counter counter)
                {
                    counter.Value = 0;
                }
            }
            """;

        var diagnostics = Run(new EncapsulationLeakRule(), source);
        AssertRuleCount(diagnostics, "OOP106", 0);
    }

    private static void StaticMemberCandidateIsDetected()
    {
        const string source = """
            internal sealed class Calculator
            {
                private int _offset;

                public int Add(int left, int right) => left + right;
                public int AddOffset(int value) => value + _offset;
            }
            """;

        var diagnostics = Run(new StaticMemberCandidateRule(), source);
        AssertSingleRule(diagnostics, "OOP103", DesignDiagnosticSeverity.Attention);
    }

    private static void InheritedInstanceStateIsNotStaticCandidate()
    {
        const string source = """
            internal class BaseCalculator
            {
                protected int Offset => 1;
            }

            internal sealed class Calculator : BaseCalculator
            {
                public int AddOffset(int value) => value + Offset;
            }
            """;

        var diagnostics = Run(new StaticMemberCandidateRule(), source);
        AssertRuleCount(diagnostics, "OOP103", 0);
    }

    private static void StaticClassCandidateIsDetected()
    {
        const string source = """
            internal class Utility
            {
                public int Add(int left, int right) => left + right;
            }
            """;

        var diagnostics = Run(new StaticClassCandidateRule(), source);
        AssertSingleRule(diagnostics, "OOP104", DesignDiagnosticSeverity.Attention);
    }

    private static void TypeBranchingIsDetected()
    {
        const string source = """
            internal abstract class Animal
            {
            }

            internal sealed class Dog : Animal
            {
            }

            internal sealed class Cat : Animal
            {
            }

            internal sealed class Handler
            {
                public void Handle(Animal animal)
                {
                    if (animal is Dog)
                    {
                    }
                    else if (animal is Cat)
                    {
                    }
                }
            }
            """;

        var diagnostics = Run(new TypeBranchPolymorphismRule(), source);
        AssertSingleRule(diagnostics, "OOP002", DesignDiagnosticSeverity.Warning);
    }

    private static void DisabledParentBehaviorIsDetected()
    {
        const string source = """
            using System;

            internal abstract class Base
            {
                public abstract void Run();
            }

            internal sealed class Child : Base
            {
                public override void Run() => throw new NotSupportedException();
            }
            """;

        var diagnostics = Run(new ChildDisablesParentBehaviorRule(), source);
        AssertSingleRule(diagnostics, "OOP303", DesignDiagnosticSeverity.Danger);
    }

    private static void DeepInheritanceIsDetected()
    {
        const string source = """
            internal class A
            {
            }

            internal class B : A
            {
            }

            internal class C : B
            {
            }

            internal class D : C
            {
            }

            internal sealed class E : D
            {
            }
            """;

        var diagnostics = Run(new ExcessiveInheritanceDepthRule(), source);
        AssertSingleRule(diagnostics, "OOP304", DesignDiagnosticSeverity.Attention);
    }

    private static DesignDiagnostic[] Run(IAnalysisRule rule, string source) =>
        rule.Analyze(TestProjectFactory.Create(source)).ToArray();

    private static string FindRepositoryRoot()
    {
        var candidates = new[] { Directory.GetCurrentDirectory(), AppContext.BaseDirectory };

        foreach (var candidate in candidates)
        {
            var current = new DirectoryInfo(candidate);
            while (current is not null)
            {
                if (File.Exists(Path.Combine(current.FullName, "OopDesignChecker.sln")))
                {
                    return current.FullName;
                }

                current = current.Parent;
            }
        }

        throw new InvalidOperationException(
            "Could not locate repository root for integration test."
        );
    }

    private static void AssertSingleRule(
        DesignDiagnostic[] diagnostics,
        string ruleId,
        DesignDiagnosticSeverity severity
    )
    {
        AssertRuleCount(diagnostics, ruleId, 1);

        if (diagnostics[0].Severity != severity)
        {
            throw new InvalidOperationException(
                $"Expected severity {severity}, but found {diagnostics[0].Severity}."
            );
        }
    }

    private static void AssertRuleCount(
        DesignDiagnostic[] diagnostics,
        string ruleId,
        int expectedCount
    )
    {
        var matching = diagnostics.Where(diagnostic => diagnostic.Rule.Id == ruleId).ToArray();
        if (matching.Length != expectedCount)
        {
            var found = string.Join(", ", diagnostics.Select(diagnostic => diagnostic.Rule.Id));
            throw new InvalidOperationException(
                $"Expected {expectedCount} {ruleId} diagnostic(s), found {matching.Length}. All diagnostics: [{found}]"
            );
        }
    }

    private sealed record TestCase(string Name, Action Run);
}
