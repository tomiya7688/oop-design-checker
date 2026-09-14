using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using OopDesignChecker.Analysis;
using OopDesignChecker.Core;

namespace OopDesignChecker.Rules;

internal sealed class ExcessiveInheritanceDepthRule : IAnalysisRule
{
    private const int WarningDepth = 4;

    public RuleDescriptor Descriptor { get; } = new(
        "OOP304",
        "Excessive inheritance depth",
        DesignDiagnosticSeverity.Attention);

    public IEnumerable<DesignDiagnostic> Analyze(AnalysisContext context)
    {
        foreach (var syntaxTree in context.Project.SyntaxTrees)
        {
            var semanticModel = context.Project.GetSemanticModel(syntaxTree);
            var root = syntaxTree.GetRoot();

            foreach (var declaration in root.DescendantNodes().OfType<TypeDeclarationSyntax>())
            {
                if (semanticModel.GetDeclaredSymbol(declaration) is not INamedTypeSymbol symbol
                    || !SymbolUtilities.IsPrimaryDeclaration(symbol, declaration))
                {
                    continue;
                }

                var depth = SymbolUtilities.GetInheritanceDepth(symbol);
                if (depth < WarningDepth)
                {
                    continue;
                }

                yield return DiagnosticFactory.Create(
                    Descriptor,
                    declaration.Identifier.GetLocation(),
                    $"Inheritance depth is {depth}. Deep inheritance makes behavior harder to reason about.",
                    symbol.ToDisplayString());
            }
        }
    }
}
