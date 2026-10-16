using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using OopDesignChecker.Analysis;
using OopDesignChecker.Core;

namespace OopDesignChecker.Rules;

internal sealed class SealingCandidateRule : I解析規則
{
    public RuleDescriptor 規則情報 { get; } =
        new("OOP102", "Sealing candidate", DesignDiagnosticSeverity.Attention);

    public IEnumerable<DesignDiagnostic> 解析する(AnalysisContext context)
    {
        if (!context.Project.アプリケーションである)
        {
            yield break;
        }

        var declarations = context
            .Project.構文木群.SelectMany(tree =>
                tree.GetRoot().DescendantNodes().OfType<ClassDeclarationSyntax>()
            )
            .Select(declaration => new TypeDeclaration(
                declaration,
                context
                    .Project.意味モデルを取得する(declaration.SyntaxTree)
                    .GetDeclaredSymbol(declaration) as INamedTypeSymbol
            ))
            .Where(item => item.Symbol is not null)
            .ToArray();

        var inheritedTypes = declarations
            .Select(item => item.Symbol!.BaseType)
            .Where(baseType => baseType is not null)
            .ToHashSet(SymbolEqualityComparer.Default);

        foreach (var item in declarations)
        {
            var symbol = item.Symbol!;
            if (
                !SymbolUtilities.IsPrimaryDeclaration(symbol, item.Declaration)
                || symbol.IsAbstract
                || symbol.IsStatic
                || symbol.IsSealed
                || symbol.DeclaredAccessibility == Accessibility.Public
                || inheritedTypes.Contains(symbol)
                || HasInheritanceIntent(symbol)
                || FrameworkContractClassifier.HasExternalTypeContract(symbol)
            )
            {
                continue;
            }

            yield return 診断生成器.生成する(
                規則情報,
                item.Declaration.Identifier.GetLocation(),
                "This application class has no known derived type or inheritance contract. Consider sealing it to make the design intent explicit.",
                symbol.ToDisplayString()
            );
        }
    }

    private static bool HasInheritanceIntent(INamedTypeSymbol symbol) =>
        symbol
            .GetMembers()
            .Any(member =>
                member.DeclaredAccessibility
                    is Accessibility.Protected
                        or Accessibility.ProtectedOrInternal
                || member is IMethodSymbol { IsVirtual: true }
            );

    private sealed record TypeDeclaration(
        ClassDeclarationSyntax Declaration,
        INamedTypeSymbol? Symbol
    );
}
