using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace OopDesignChecker.Analysis;

internal static class シンボル操作器
{
    public static bool 主宣言である(INamedTypeSymbol symbol, TypeDeclarationSyntax declaration)
    {
        var firstReference = symbol.DeclaringSyntaxReferences.FirstOrDefault();
        if (firstReference is null)
        {
            return false;
        }

        return firstReference.SyntaxTree == declaration.SyntaxTree
            && firstReference.Span == declaration.Span;
    }

    public static int 継承深度を取得する(INamedTypeSymbol symbol) => 継承深度を取得する(symbol, []);

    public static int 継承深度を取得する(
        INamedTypeSymbol symbol,
        IReadOnlyList<解析対象プロジェクト> projects
    )
    {
        var depth = 0;
        var current = symbol.BaseType;

        while (
            current is not null
            && current.SpecialType != SpecialType.System_Object
            && プロジェクト内の型である(current, projects)
        )
        {
            depth++;
            current = current.BaseType;
        }

        return depth;
    }

    private static bool プロジェクト内の型である(
        INamedTypeSymbol type,
        IReadOnlyList<解析対象プロジェクト> projects
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
            project.コンパイル結果.Assembly.Identity.Equals(type.ContainingAssembly.Identity)
        );
    }

    public static bool インターフェースメンバーを実装している(
        IMethodSymbol method,
        ClassDeclarationSyntax declaration,
        SemanticModel semanticModel
    )
    {
        foreach (var interfaceType in method.ContainingType.AllInterfaces)
        {
            if (一致するインターフェースメソッドがある(method, interfaceType))
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
                一致するインターフェースメソッドがある(method, type)
                || type.AllInterfaces.Any(interfaceType =>
                    一致するインターフェースメソッドがある(method, interfaceType)
                )
            )
            {
                return true;
            }
        }

        return false;
    }

    private static bool 一致するインターフェースメソッドがある(
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

    public static bool 同じ型か基底型である(INamedTypeSymbol possibleBase, INamedTypeSymbol type)
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
