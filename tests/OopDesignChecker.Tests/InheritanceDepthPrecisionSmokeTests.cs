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
