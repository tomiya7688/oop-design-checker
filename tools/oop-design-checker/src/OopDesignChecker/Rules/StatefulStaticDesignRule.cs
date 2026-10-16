using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using OopDesignChecker.Analysis;
using OopDesignChecker.Core;

namespace OopDesignChecker.Rules;

internal sealed class StatefulStaticDesignRule : I解析規則
{
    public RuleDescriptor 規則情報 { get; } =
        new("OOP105", "Stateful static design", DesignDiagnosticSeverity.Warning);

    public IEnumerable<DesignDiagnostic> 解析する(解析文脈 context)
    {
        foreach (var syntaxTree in context.対象プロジェクト.構文木群)
        {
            var semanticModel = context.対象プロジェクト.意味モデルを取得する(syntaxTree);
            var root = syntaxTree.GetRoot();

            foreach (var declaration in root.DescendantNodes().OfType<ClassDeclarationSyntax>())
            {
                if (
                    semanticModel.GetDeclaredSymbol(declaration)
                        is not INamedTypeSymbol { IsStatic: true } symbol
                    || !シンボル操作器.主宣言である(symbol, declaration)
                )
                {
                    continue;
                }

                var mutableFields = symbol
                    .GetMembers()
                    .OfType<IFieldSymbol>()
                    .Where(field =>
                        field.IsStatic
                        && !field.IsConst
                        && !field.IsImplicitlyDeclared
                        && !IsThreadLocal(field)
                        && (
                            !field.IsReadOnly
                            || MutableCollectionInspector.IsMutableCollection(field.Type)
                        )
                    )
                    .Cast<ISymbol>();

                var mutableProperties = symbol
                    .GetMembers()
                    .OfType<IPropertySymbol>()
                    .Where(property =>
                        property.IsStatic
                        && !property.IsImplicitlyDeclared
                        && (
                            property.SetMethod is not null
                            || MutableCollectionInspector.IsMutableCollection(property.Type)
                        )
                    )
                    .Cast<ISymbol>();

                var mutableMembers = mutableFields.Concat(mutableProperties).ToArray();
                if (mutableMembers.Length == 0)
                {
                    continue;
                }

                yield return 診断生成器.生成する(
                    規則情報,
                    declaration.Identifier.GetLocation(),
                    $"This static class owns {mutableMembers.Length} mutable shared member(s). Shared mutable state behaves like global state; prefer an object with an explicit lifetime.",
                    symbol.ToDisplayString()
                );
            }
        }
    }

    private static bool IsThreadLocal(IFieldSymbol field) =>
        field
            .GetAttributes()
            .Any(attribute =>
                attribute.AttributeClass?.ToDisplayString() == "System.ThreadStaticAttribute"
            );
}
