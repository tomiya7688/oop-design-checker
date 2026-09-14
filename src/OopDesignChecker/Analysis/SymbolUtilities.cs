using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace OopDesignChecker.Analysis;

internal static class SymbolUtilities
{
    public static bool IsPrimaryDeclaration(
        INamedTypeSymbol symbol,
        TypeDeclarationSyntax declaration
    )
    {
        var firstReference = symbol.DeclaringSyntaxReferences.FirstOrDefault();
        if (firstReference is null)
        {
            return false;
        }

        return firstReference.SyntaxTree == declaration.SyntaxTree
            && firstReference.Span == declaration.Span;
    }

    public static int GetInheritanceDepth(INamedTypeSymbol symbol)
    {
        var depth = 0;
        var current = symbol.BaseType;

        while (current is not null && current.SpecialType != SpecialType.System_Object)
        {
            depth++;
            current = current.BaseType;
        }

        return depth;
    }

    public static bool ImplementsInterfaceMember(IMethodSymbol method)
    {
        foreach (var interfaceType in method.ContainingType.AllInterfaces)
        {
            foreach (
                var interfaceMember in interfaceType.GetMembers(method.Name).OfType<IMethodSymbol>()
            )
            {
                var implementation = method.ContainingType.FindImplementationForInterfaceMember(
                    interfaceMember
                );
                if (SymbolEqualityComparer.Default.Equals(implementation, method))
                {
                    return true;
                }

                if (
                    interfaceMember.Arity == method.Arity
                    && interfaceMember.Parameters.Length == method.Parameters.Length
                )
                {
                    return true;
                }
            }
        }

        return false;
    }

    public static bool IsSameOrBaseType(INamedTypeSymbol possibleBase, INamedTypeSymbol type)
    {
        INamedTypeSymbol? current = type;
        while (current is not null)
        {
            if (SymbolEqualityComparer.Default.Equals(possibleBase, current))
            {
                return true;
            }

            current = current.BaseType;
        }

        return false;
    }
}
