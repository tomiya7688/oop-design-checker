using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using OopDesignChecker.Analysis;
using OopDesignChecker.Core;

namespace OopDesignChecker.Rules;

internal sealed class ExcessiveInheritanceDepthRule : IAnalysisRule
{
    private readonly int _warningDepth;

    public ExcessiveInheritanceDepthRule(int warningDepth = 4)
    {
        if (warningDepth < 1)
        {
            throw new ArgumentOutOfRangeException(
                nameof(warningDepth),
                warningDepth,
                "Inheritance warning depth must be at least 1."
            );
        }

        _warningDepth = warningDepth;
    }

    public RuleDescriptor Descriptor { get; } =
        new("OOP304", "Excessive inheritance depth", DesignDiagnosticSeverity.Attention);

    public IEnumerable<DesignDiagnostic> Analyze(AnalysisContext context)
    {
        foreach (var syntaxTree in context.Project.SyntaxTrees)
        {
            var semanticModel = context.Project.GetSemanticModel(syntaxTree);
            var root = syntaxTree.GetRoot();

            foreach (var declaration in root.DescendantNodes().OfType<TypeDeclarationSyntax>())
            {
                if (
                    semanticModel.GetDeclaredSymbol(declaration) is not INamedTypeSymbol symbol
                    || !SymbolUtilities.IsPrimaryDeclaration(symbol, declaration)
                )
                {
                    continue;
                }

                var depth = SymbolUtilities.GetInheritanceDepth(symbol, context.Projects);
                if (depth < _warningDepth)
                {
                    continue;
                }

                yield return DiagnosticFactory.Create(
                    Descriptor,
                    declaration.Identifier.GetLocation(),
                    $"Project-owned inheritance depth is {depth} (configured attention threshold: {_warningDepth}). Deep inheritance can make behavior harder to reason about.",
                    symbol.ToDisplayString()
                );
            }
        }
    }
}
