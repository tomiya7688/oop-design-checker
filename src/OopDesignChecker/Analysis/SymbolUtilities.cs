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

    public static int GetInheritanceDepth(INamedTypeSymbol symbol) =>
        GetInheritanceDepth(symbol, []);

    public static int GetInheritanceDepth(
        INamedTypeSymbol symbol,
        IReadOnlyList<SourceProject> projects
    )
    {
        var depth = 0;
        var current = symbol.BaseType;

        while (
            current is not null
            && current.SpecialType != SpecialType.System_Object
            && IsProjectOwnedType(current, projects)
        )
        {
            depth++;
            current = current.BaseType;
        }

        return depth;
    }

    private static bool IsProjectOwnedType(
        INamedTypeSymbol type,
        IReadOnlyList<SourceProject> projects
    )
    {
        if (type.Locations.Any(location => location.IsInSource))
        {
            return true;
        }

        if (type.ContainingAssembly is null)
        {
            return false;
        }

        return projects.Any(project =>
            project.Compilation.Assembly.Identity.Equals(type.ContainingAssembly.Identity)
        );
    }

    public static bool ImplementsInterfaceMember(
        IMethodSymbol method,
        ClassDeclarationSyntax declaration,
        SemanticModel semanticModel
    )
    {
        foreach (var interfaceType in method.ContainingType.AllInterfaces)
        {
            if (HasMatchingInterfaceMethod(method, interfaceType))
            {
                return true;
            }
        }

        if (declaration.BaseList is null)
        {
            return false;
        }

        foreach (var baseType in declaration.BaseList.Types)
        {
            if (
                semanticModel.GetTypeInfo(baseType.Type).Type is not INamedTypeSymbol type
                || type.TypeKind != TypeKind.Interface
            )
            {
                continue;
            }

            if (
                HasMatchingInterfaceMethod(method, type)
                || type.AllInterfaces.Any(interfaceType =>
                    HasMatchingInterfaceMethod(method, interfaceType)
                )
            )
            {
                return true;
            }
        }

        return false;
    }

    private static bool HasMatchingInterfaceMethod(
        IMethodSymbol method,
        INamedTypeSymbol interfaceType
    )
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
