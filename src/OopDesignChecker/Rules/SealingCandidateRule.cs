using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using OopDesignChecker.Analysis;
using OopDesignChecker.Core;

namespace OopDesignChecker.Rules;

internal sealed class SealingCandidateRule : IAnalysisRule
{
    public RuleDescriptor Descriptor { get; } = new(
        "OOP102",
        "Sealing candidate",
        DesignDiagnosticSeverity.Attention);

    public IEnumerable<DesignDiagnostic> Analyze(AnalysisContext context)
    {
        if (!context.Project.IsApplication)
        {
            yield break;
        }

        var declarations = context.Project.SyntaxTrees
            .SelectMany(tree => tree.GetRoot().DescendantNodes().OfType<ClassDeclarationSyntax>())
            .Select(declaration => new TypeDeclaration(
                declaration,
                context.Project.GetSemanticModel(declaration.SyntaxTree).GetDeclaredSymbol(declaration) as INamedTypeSymbol))
            .Where(item => item.Symbol is not null)
            .ToArray();

        var inheritedTypes = declarations
            .Select(item => item.Symbol!.BaseType)
            .Where(baseType => baseType is not null)
            .ToHashSet(SymbolEqualityComparer.Default);

        foreach (var item in declarations)
        {
            var symbol = item.Symbol!;
            if (!SymbolUtilities.IsPrimaryDeclaration(symbol, item.Declaration)
                || symbol.IsAbstract
                || symbol.IsStatic
                || symbol.IsSealed
                || symbol.DeclaredAccessibility == Accessibility.Public
                || inheritedTypes.Contains(symbol)
                || HasInheritanceIntent(symbol))
            {
                continue;
            }

            yield return DiagnosticFactory.Create(
                Descriptor,
                item.Declaration.Identifier.GetLocation(),
                "This application class has no known derived type or inheritance contract. Consider sealing it to make the design intent explicit.",
                symbol.ToDisplayString());
        }
    }

    private static bool HasInheritanceIntent(INamedTypeSymbol symbol) =>
        symbol.GetMembers().Any(member =>
            member.DeclaredAccessibility is Accessibility.Protected or Accessibility.ProtectedOrInternal
            || member is IMethodSymbol { IsVirtual: true });

    private sealed record TypeDeclaration(
        ClassDeclarationSyntax Declaration,
        INamedTypeSymbol? Symbol);
}
