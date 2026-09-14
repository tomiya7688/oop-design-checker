using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using OopDesignChecker.Analysis;
using OopDesignChecker.Core;

namespace OopDesignChecker.Rules;

internal sealed class CompositionCandidateRule : IAnalysisRule
{
    private const int MinimumProtectedMembersUsed = 2;

    public RuleDescriptor Descriptor { get; } =
        new(
            "OOP307",
            "Composition may be more appropriate than inheritance",
            DesignDiagnosticSeverity.Attention
        );

    public IEnumerable<DesignDiagnostic> Analyze(AnalysisContext context)
    {
        foreach (var syntaxTree in context.Project.SyntaxTrees)
        {
            var semanticModel = context.Project.GetSemanticModel(syntaxTree);
            var root = syntaxTree.GetRoot();

            foreach (var declaration in root.DescendantNodes().OfType<ClassDeclarationSyntax>())
            {
                if (
                    semanticModel.GetDeclaredSymbol(declaration) is not INamedTypeSymbol symbol
                    || symbol.BaseType is not { } baseType
                    || baseType.SpecialType == SpecialType.System_Object
                    || baseType.IsAbstract
                    || !baseType.Locations.Any(location => location.IsInSource)
                    || declaration
                        .Members.OfType<MethodDeclarationSyntax>()
                        .Any(method =>
                            semanticModel.GetDeclaredSymbol(method)
                                is IMethodSymbol { IsOverride: true }
                        )
                )
                {
                    continue;
                }

                var usedProtectedMembers = declaration
                    .DescendantNodes()
                    .OfType<SimpleNameSyntax>()
                    .Select(name => semanticModel.GetSymbolInfo(name).Symbol)
                    .Where(member => IsProtectedBaseMember(member, baseType))
                    .Select(member => member!.Name)
                    .Distinct(StringComparer.Ordinal)
                    .ToArray();
                if (usedProtectedMembers.Length < MinimumProtectedMembersUsed)
                {
                    continue;
                }

                yield return DiagnosticFactory.Create(
                    Descriptor,
                    declaration.Identifier.GetLocation(),
                    $"This type does not specialize parent behavior but directly reuses {usedProtectedMembers.Length} protected members from concrete base {baseType.Name}. Composition may express the relationship more accurately.",
                    symbol.ToDisplayString()
                );
            }
        }
    }

    private static bool IsProtectedBaseMember(ISymbol? member, INamedTypeSymbol baseType) =>
        member is not null
        && member.DeclaredAccessibility
            is Accessibility.Protected
                or Accessibility.ProtectedOrInternal
        && member.ContainingType is not null
        && SymbolUtilities.IsSameOrBaseType(member.ContainingType, baseType);
}
