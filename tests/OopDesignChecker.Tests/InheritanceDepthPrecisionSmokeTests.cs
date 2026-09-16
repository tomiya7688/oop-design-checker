using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using OopDesignChecker.Analysis;
using OopDesignChecker.Core;
using OopDesignChecker.Rules;

namespace OopDesignChecker.Tests;

internal static class InheritanceDepthPrecisionSmokeTests
{
    public static void Run()
    {
        DefaultDepthFourIsAttention();
        DepthBelowDefaultIsAllowed();
        CustomDepthChangesTriggerPoint();
        ConfiguredDepthIsAppliedByCheckerService();
        InvalidConfiguredDepthIsRejected();
        ExternalAncestryIsNotCounted();
        CrossProjectInheritanceIsCounted();
        CrossProjectChainStopsAtExternalBase();
    }

    private static void DefaultDepthFourIsAttention()
    {
        const string source = """
            internal class A { }
            internal class B : A { }
            internal class C : B { }
            internal class D : C { }
            internal sealed class E : D { }
            """;

        AssertSingle(
            new ExcessiveInheritanceDepthRule(),
            TestProjectFactory.Create(source),
            expectedDepth: 4
        );
    }

    private static void DepthBelowDefaultIsAllowed()
    {
        const string source = """
            internal class A { }
            internal class B : A { }
            internal class C : B { }
            internal sealed class D : C { }
            """;

        AssertNone(new ExcessiveInheritanceDepthRule(), TestProjectFactory.Create(source));
    }

    private static void CustomDepthChangesTriggerPoint()
    {
        const string source = """
            internal class A { }
            internal class B : A { }
            internal class C : B { }
            internal sealed class D : C { }
            """;

        AssertSingle(
            new ExcessiveInheritanceDepthRule(warningDepth: 3),
            TestProjectFactory.Create(source),
            expectedDepth: 3
        );
    }

    private static void ConfiguredDepthIsAppliedByCheckerService()
    {
        WithTemporaryProject(rootPath =>
        {
            File.WriteAllText(
                Path.Combine(rootPath, "Sample.cs"),
                """
                internal class A { }
                internal class B : A { }
                internal class C : B { }
                internal sealed class D : C { }
                """
            );
            File.WriteAllText(
                Path.Combine(rootPath, "oop-design-checker.json"),
                """
                {
                  "ruleSettings": {
                    "oop304": {
                      "warningDepth": 3
                    }
                  }
                }
                """
            );

            var result = CheckerService.Analyze(rootPath);
            var diagnostics = result.Diagnostics.Where(item => item.Rule.Id == "OOP304").ToArray();
            if (
                diagnostics.Length == 1
                && diagnostics[0]
                    .Message.Contains("configured attention threshold: 3", StringComparison.Ordinal)
            )
            {
                return;
            }

            throw new InvalidOperationException(
                $"Expected configured OOP304 threshold 3 to produce one diagnostic, found {diagnostics.Length}."
            );
        });
    }

    private static void InvalidConfiguredDepthIsRejected()
    {
        WithTemporaryProject(rootPath =>
        {
            File.WriteAllText(
                Path.Combine(rootPath, "Sample.cs"),
                "internal sealed class Sample { }"
            );
            File.WriteAllText(
                Path.Combine(rootPath, "oop-design-checker.json"),
                """
                {
                  "ruleSettings": {
                    "oop304": {
                      "warningDepth": 0
                    }
                  }
                }
                """
            );

            try
            {
                _ = CheckerService.Analyze(rootPath);
            }
            catch (InvalidOperationException exception)
                when (exception.Message.Contains(
                        "ruleSettings.oop304.warningDepth must be at least 1",
                        StringComparison.Ordinal
                    )
                )
            {
                return;
            }

            throw new InvalidOperationException(
                "Invalid OOP304 warningDepth was not rejected with a clear configuration error."
            );
        });
    }

    private static void ExternalAncestryIsNotCounted()
    {
        const string source = """
            internal class A : System.Exception { }
            internal class B : A { }
            internal class C : B { }
            internal class D : C { }
            """;

        AssertNone(new ExcessiveInheritanceDepthRule(), TestProjectFactory.Create(source));
    }

    private static void CrossProjectInheritanceIsCounted()
    {
        var baseProject = CreateProject(
            "BaseProject",
            """
            public class A { }
            public class B : A { }
            """
        );
        var derivedProject = CreateProject(
            "DerivedProject",
            """
            public class C : B { }
            public class D : C { }
            public sealed class E : D { }
            """,
            baseProject.Compilation.ToMetadataReference()
        );
        var context = new AnalysisContext(derivedProject, [baseProject, derivedProject]);

        AssertSingle(new ExcessiveInheritanceDepthRule(), context, expectedDepth: 4);
    }

    private static void CrossProjectChainStopsAtExternalBase()
    {
        var baseProject = CreateProject(
            "BaseProject",
            """
            public class A : System.Exception { }
            public class B : A { }
            """
        );
        var derivedProject = CreateProject(
            "DerivedProject",
            """
            public class C : B { }
            public sealed class D : C { }
            """,
            baseProject.Compilation.ToMetadataReference()
        );
        var context = new AnalysisContext(derivedProject, [baseProject, derivedProject]);

        AssertNone(new ExcessiveInheritanceDepthRule(), context);
    }

    private static SourceProject CreateProject(
        string assemblyName,
        string source,
        params MetadataReference[] additionalReferences
    )
    {
        var syntaxTree = CSharpSyntaxTree.ParseText(source, path: $"{assemblyName}.cs");
        var references = MetadataReferenceProvider
            .CreatePlatformReferences()
            .Concat(additionalReferences)
            .ToArray();
        var compilation = CSharpCompilation.Create(
            assemblyName,
            [syntaxTree],
            references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary)
        );
        var errors = compilation
            .GetDiagnostics()
            .Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error)
            .ToArray();
        if (errors.Length > 0)
        {
            throw new InvalidOperationException(
                "Inheritance-depth test project did not compile:\n"
                    + string.Join("\n", errors.Select(error => error.ToString()))
            );
        }

        var semanticModels = new Dictionary<SyntaxTree, SemanticModel>
        {
            [syntaxTree] = compilation.GetSemanticModel(syntaxTree),
        };
        return new SourceProject(
            rootPath: assemblyName,
            isApplication: false,
            compilation,
            semanticModels
        );
    }

    private static void WithTemporaryProject(Action<string> action)
    {
        var rootPath = Path.Combine(
            Path.GetTempPath(),
            "oop-design-checker-tests",
            Guid.NewGuid().ToString("N")
        );
        Directory.CreateDirectory(rootPath);

        try
        {
            action(rootPath);
        }
        finally
        {
            if (Directory.Exists(rootPath))
            {
                Directory.Delete(rootPath, recursive: true);
            }
        }
    }

    private static void AssertSingle(
        ExcessiveInheritanceDepthRule rule,
        AnalysisContext context,
        int expectedDepth
    )
    {
        var diagnostics = rule.Analyze(context).ToArray();
        if (
            diagnostics.Length == 1
            && diagnostics[0].Rule.Id == "OOP304"
            && diagnostics[0].Severity == DesignDiagnosticSeverity.Attention
            && diagnostics[0].Message.Contains(
                $"Project-owned inheritance depth is {expectedDepth}",
                StringComparison.Ordinal
            )
        )
        {
            return;
        }

        var found = string.Join(
            " | ",
            diagnostics.Select(diagnostic =>
                $"{diagnostic.Rule.Id}:{diagnostic.Severity}:{diagnostic.Message}"
            )
        );
        throw new InvalidOperationException(
            $"Expected one OOP304 Attention at depth {expectedDepth}, found [{found}]."
        );
    }

    private static void AssertNone(ExcessiveInheritanceDepthRule rule, AnalysisContext context)
    {
        var diagnostics = rule.Analyze(context).Where(item => item.Rule.Id == "OOP304").ToArray();
        if (diagnostics.Length == 0)
        {
            return;
        }

        throw new InvalidOperationException(
            $"Expected no OOP304 diagnostic, found {diagnostics.Length}."
        );
    }
}
