using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using OopDesignChecker.Analysis;

namespace OopDesignChecker.Tests;

internal static class TestProjectFactory
{
    public static AnalysisContext Create(string source)
    {
        var syntaxTree = CSharpSyntaxTree.ParseText(source, path: "Test.cs");
        var compilation = CSharpCompilation.Create(
            "RuleTests",
            [syntaxTree],
            MetadataReferenceProvider.CreatePlatformReferences(),
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary)
        );

        var errors = compilation
            .GetDiagnostics()
            .Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error)
            .ToArray();

        if (errors.Length > 0)
        {
            throw new InvalidOperationException(
                "Test source did not compile:\n"
                    + string.Join("\n", errors.Select(error => error.ToString()))
            );
        }

        var semanticModels = new Dictionary<SyntaxTree, SemanticModel>
        {
            [syntaxTree] = compilation.GetSemanticModel(syntaxTree),
        };

        var project = new SourceProject(
            rootPath: ".",
            isApplication: true,
            compilation,
            semanticModels
        );

        return new AnalysisContext(project);
    }
}
