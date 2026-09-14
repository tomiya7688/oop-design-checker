using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using OopDesignChecker.Analysis;
using OopDesignChecker.Core;

namespace OopDesignChecker.Rules;

internal sealed class OversizedMainOperationRule : IAnalysisRule
{
    private const int LargeMethodLineCount = 120;
    private const int ComplexMethodLineCount = 60;
    private const int HighComplexity = 20;

    public RuleDescriptor Descriptor { get; } =
        new("OOP201", "Oversized main operation", DesignDiagnosticSeverity.Warning);

    public IEnumerable<DesignDiagnostic> Analyze(AnalysisContext context)
    {
        foreach (var syntaxTree in context.Project.SyntaxTrees)
        {
            var semanticModel = context.Project.GetSemanticModel(syntaxTree);
            var root = syntaxTree.GetRoot();

            foreach (var method in root.DescendantNodes().OfType<MethodDeclarationSyntax>())
            {
                if (semanticModel.GetDeclaredSymbol(method) is not IMethodSymbol methodSymbol)
                {
                    continue;
                }

                var lineSpan = method.GetLocation().GetLineSpan();
                var lineCount = lineSpan.EndLinePosition.Line - lineSpan.StartLinePosition.Line + 1;
                var complexity = MethodComplexityCalculator.Calculate(method);

                if (
                    lineCount < LargeMethodLineCount
                    && (lineCount < ComplexMethodLineCount || complexity < HighComplexity)
                )
                {
                    continue;
                }

                yield return DiagnosticFactory.Create(
                    Descriptor,
                    method.Identifier.GetLocation(),
                    $"This operation is {lineCount} lines with estimated cyclomatic complexity {complexity}. Split meaningful processing phases into methods.",
                    methodSymbol.ToDisplayString()
                );
            }
        }
    }
}
