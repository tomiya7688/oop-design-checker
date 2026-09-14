using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace OopDesignChecker.Analysis;

internal static class InstanceUsageInspector
{
    public static bool UsesInstanceState(
        MethodDeclarationSyntax method,
        IMethodSymbol symbol,
        SemanticModel semanticModel
    )
    {
        if (
            method
                .DescendantNodes()
                .Any(node => node is ThisExpressionSyntax or BaseExpressionSyntax)
        )
        {
            return true;
        }

        foreach (var identifier in method.DescendantNodes().OfType<IdentifierNameSyntax>())
        {
            var referencedSymbol = semanticModel.GetSymbolInfo(identifier).Symbol;
            if (referencedSymbol is null || referencedSymbol.IsStatic)
            {
                continue;
            }

            if (
                referencedSymbol is IFieldSymbol or IPropertySymbol or IEventSymbol or IMethodSymbol
            )
            {
                if (
                    SymbolEqualityComparer.Default.Equals(
                        referencedSymbol.ContainingType,
                        symbol.ContainingType
                    )
                )
                {
                    return true;
                }
            }
        }

        return false;
    }
}
