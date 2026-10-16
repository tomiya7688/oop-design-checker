using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace OopDesignChecker.Analysis;

internal static class インスタンス使用検査器
{
    public static bool インスタンス状態を使う(
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
                referencedSymbol
                    is not (IFieldSymbol or IPropertySymbol or IEventSymbol or IMethodSymbol)
                || referencedSymbol.ContainingType is null
            )
            {
                continue;
            }

            if (
                シンボル操作器.同じ型か基底型である(
                    referencedSymbol.ContainingType,
                    symbol.ContainingType
                )
            )
            {
                return true;
            }
        }

        return false;
    }
}
