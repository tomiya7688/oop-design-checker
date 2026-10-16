using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using OopDesignChecker.Analysis;
using OopDesignChecker.Core;

namespace OopDesignChecker.Rules;

internal sealed class ExcessiveVisibilityRule : I解析規則
{
    public RuleDescriptor 規則情報 { get; } =
        new("OOP101", "Excessive visibility", DesignDiagnosticSeverity.Warning);

    public IEnumerable<DesignDiagnostic> 解析する(解析文脈 context)
    {
        if (!context.対象プロジェクト.アプリケーションである)
        {
            yield break;
        }

        foreach (var syntaxTree in context.対象プロジェクト.構文木群)
        {
            var semanticModel = context.対象プロジェクト.意味モデルを取得する(syntaxTree);
            var root = syntaxTree.GetRoot();

            foreach (var declaration in root.DescendantNodes().OfType<ClassDeclarationSyntax>())
            {
                if (
                    semanticModel.GetDeclaredSymbol(declaration) is not INamedTypeSymbol symbol
                    || symbol.DeclaredAccessibility != Accessibility.Public
                    || !シンボル操作器.主宣言である(symbol, declaration)
                    || FrameworkContractClassifier.HasExternalTypeContract(symbol)
                )
                {
                    continue;
                }

                var referencingTypes = FindReferencingTypes(context, symbol)
                    .Where(type => !SymbolEqualityComparer.Default.Equals(type, symbol))
                    .ToArray();

                if (
                    referencingTypes.Length == 0
                    || referencingTypes.Any(type => !IsHierarchyRelated(symbol, type))
                )
                {
                    continue;
                }

                yield return 診断生成器.生成する(
                    規則情報,
                    declaration.Identifier.GetLocation(),
                    "This public class is referenced only inside its inheritance hierarchy. A narrower visibility is likely sufficient.",
                    symbol.ToDisplayString()
                );
            }
        }
    }

    private static List<INamedTypeSymbol> FindReferencingTypes(
        解析文脈 context,
        INamedTypeSymbol target
    )
    {
        var result = new List<INamedTypeSymbol>();

        foreach (var syntaxTree in context.対象プロジェクト.構文木群)
        {
            var semanticModel = context.対象プロジェクト.意味モデルを取得する(syntaxTree);
            var root = syntaxTree.GetRoot();

            foreach (var name in root.DescendantNodes().OfType<SimpleNameSyntax>())
            {
                var referencedSymbol = semanticModel.GetSymbolInfo(name).Symbol;
                if (!SymbolEqualityComparer.Default.Equals(referencedSymbol, target))
                {
                    continue;
                }

                var containingDeclaration = name.Ancestors()
                    .OfType<TypeDeclarationSyntax>()
                    .FirstOrDefault();
                if (
                    containingDeclaration is null
                    || semanticModel.GetDeclaredSymbol(containingDeclaration)
                        is not INamedTypeSymbol referencingType
                    || result.Any(existing =>
                        SymbolEqualityComparer.Default.Equals(existing, referencingType)
                    )
                )
                {
                    continue;
                }

                result.Add(referencingType);
            }
        }

        return result;
    }

    private static bool IsHierarchyRelated(INamedTypeSymbol target, INamedTypeSymbol other) =>
        シンボル操作器.同じ型か基底型である(target, other)
        || シンボル操作器.同じ型か基底型である(other, target);
}
