using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using OopDesignChecker.Analysis;
using OopDesignChecker.Core;

namespace OopDesignChecker.Rules;

internal sealed class ExcessiveVisibilityRule : IAnalysisRule
{
    public RuleDescriptor Descriptor { get; } = new(
        "OOP101",
        "Excessive visibility",
        DesignDiagnosticSeverity.Warning);

    public IEnumerable<DesignDiagnostic> Analyze(AnalysisContext context)
    {
        if (!context.Project.IsApplication)
        {
            yield break;
        }

        foreach (var syntaxTree in context.Project.SyntaxTrees)
        {
            var semanticModel = context.Project.GetSemanticModel(syntaxTree);
            var root = syntaxTree.GetRoot();

            foreach (var declaration in root.DescendantNodes().OfType<ClassDeclarationSyntax>())
            {
                if (semanticModel.GetDeclaredSymbol(declaration) is not INamedTypeSymbol symbol
                    || symbol.DeclaredAccessibility != Accessibility.Public
                    || !SymbolUtilities.IsPrimaryDeclaration(symbol, declaration))
                {
                    continue;
                }

                var referencingTypes = FindReferencingTypes(context, symbol)
                    .Where(type => !SymbolEqualityComparer.Default.Equals(type, symbol))
                    .ToArray();

                if (referencingTypes.Length == 0
                    || referencingTypes.Any(type => !IsHierarchyRelated(symbol, type)))
                {
                    continue;
                }

                yield return DiagnosticFactory.Create(
                    Descriptor,
                    declaration.Identifier.GetLocation(),
                    "This public class is referenced only inside its inheritance hierarchy. A narrower visibility is likely sufficient.",
                    symbol.ToDisplayString());
            }
        }
    }

    private static IReadOnlyList<INamedTypeSymbol> FindReferencingTypes(
        AnalysisContext context,
        INamedTypeSymbol target)
    {
        var result = new List<INamedTypeSymbol>();

        foreach (var syntaxTree in context.Project.SyntaxTrees)
        {
            var semanticModel = context.Project.GetSemanticModel(syntaxTree);
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
                if (containingDeclaration is null
                    || semanticModel.GetDeclaredSymbol(containingDeclaration) is not INamedTypeSymbol referencingType
                    || result.Any(existing => SymbolEqualityComparer.Default.Equals(existing, referencingType)))
                {
                    continue;
                }

                result.Add(referencingType);
            }
        }

        return result;
    }

    private static bool IsHierarchyRelated(INamedTypeSymbol target, INamedTypeSymbol other) =>
        SymbolUtilities.IsSameOrBaseType(target, other)
        || SymbolUtilities.IsSameOrBaseType(other, target);
}
