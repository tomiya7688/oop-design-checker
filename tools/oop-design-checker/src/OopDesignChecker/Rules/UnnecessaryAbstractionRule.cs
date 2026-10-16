using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using OopDesignChecker.Analysis;
using OopDesignChecker.Core;

namespace OopDesignChecker.Rules;

internal sealed class UnnecessaryAbstractionRule : I解析規則
{
    public RuleDescriptor 規則情報 { get; } =
        new("OOP003", "Unnecessary abstraction candidate", DesignDiagnosticSeverity.Attention);

    public IEnumerable<DesignDiagnostic> 解析する(解析文脈 context)
    {
        var interfaces = ReadSourceInterfaces(context).ToArray();
        var classes = ReadSourceClasses(context).ToArray();

        foreach (var interfaceEntry in interfaces)
        {
            if (
                IsExternalExtensionPoint(interfaceEntry.Symbol)
                || !ProjectAbstractionClassifier.IsMeaningfulAbstraction(interfaceEntry.Symbol)
            )
            {
                continue;
            }

            var implementations = classes
                .Where(entry =>
                    entry.Symbol.AllInterfaces.Any(implemented =>
                        SymbolEqualityComparer.Default.Equals(implemented, interfaceEntry.Symbol)
                    )
                )
                .ToArray();
            if (
                implementations.Length != 1
                || HasTypedUseOutsideImplementation(
                    context,
                    interfaceEntry.Symbol,
                    implementations[0].Symbol
                )
            )
            {
                continue;
            }

            yield return 診断生成器.生成する(
                規則情報,
                interfaceEntry.Declaration.Identifier.GetLocation(),
                $"Internal abstraction {interfaceEntry.Symbol.Name} has one implementation and is not used as a declared type outside that implementation. The abstraction currently adds ceremony without enabling polymorphic use.",
                interfaceEntry.Symbol.ToDisplayString()
            );
        }
    }

    private static bool IsExternalExtensionPoint(INamedTypeSymbol interfaceType) =>
        interfaceType.DeclaredAccessibility
            is Accessibility.Public
                or Accessibility.Protected
                or Accessibility.ProtectedOrInternal
                or Accessibility.ProtectedAndInternal;

    private static IEnumerable<InterfaceEntry> ReadSourceInterfaces(解析文脈 context)
    {
        foreach (var syntaxTree in context.対象プロジェクト.構文木群)
        {
            var semanticModel = context.対象プロジェクト.意味モデルを取得する(syntaxTree);
            foreach (
                var declaration in syntaxTree
                    .GetRoot()
                    .DescendantNodes()
                    .OfType<InterfaceDeclarationSyntax>()
            )
            {
                if (semanticModel.GetDeclaredSymbol(declaration) is INamedTypeSymbol symbol)
                {
                    yield return new InterfaceEntry(declaration, symbol);
                }
            }
        }
    }

    private static IEnumerable<ClassEntry> ReadSourceClasses(解析文脈 context)
    {
        foreach (var syntaxTree in context.対象プロジェクト.構文木群)
        {
            var semanticModel = context.対象プロジェクト.意味モデルを取得する(syntaxTree);
            foreach (
                var declaration in syntaxTree
                    .GetRoot()
                    .DescendantNodes()
                    .OfType<ClassDeclarationSyntax>()
            )
            {
                if (semanticModel.GetDeclaredSymbol(declaration) is INamedTypeSymbol symbol)
                {
                    yield return new ClassEntry(symbol);
                }
            }
        }
    }

    private static bool HasTypedUseOutsideImplementation(
        解析文脈 context,
        INamedTypeSymbol interfaceType,
        INamedTypeSymbol implementation
    )
    {
        foreach (var syntaxTree in context.対象プロジェクト.構文木群)
        {
            var semanticModel = context.対象プロジェクト.意味モデルを取得する(syntaxTree);
            foreach (var name in syntaxTree.GetRoot().DescendantNodes().OfType<SimpleNameSyntax>())
            {
                if (
                    !SymbolEqualityComparer.Default.Equals(
                        semanticModel.GetSymbolInfo(name).Symbol,
                        interfaceType
                    )
                )
                {
                    continue;
                }

                if (name.Ancestors().OfType<BaseListSyntax>().Any())
                {
                    continue;
                }

                var containingType = semanticModel
                    .GetEnclosingSymbol(name.SpanStart)
                    ?.ContainingType;
                if (!SymbolEqualityComparer.Default.Equals(containingType, implementation))
                {
                    return true;
                }
            }
        }

        return false;
    }

    private sealed record InterfaceEntry(
        InterfaceDeclarationSyntax Declaration,
        INamedTypeSymbol Symbol
    );

    private sealed record ClassEntry(INamedTypeSymbol Symbol);
}
