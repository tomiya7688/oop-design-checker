using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using OopDesignChecker.Analysis;
using OopDesignChecker.Core;

namespace OopDesignChecker.Rules;

internal sealed class SuspiciousInheritanceRelationshipRule : I解析規則
{
    private const int MinimumHiddenMembers = 2;

    public RuleDescriptor 規則情報 { get; } =
        new("OOP301", "Suspicious inheritance relationship", DesignDiagnosticSeverity.Warning);

    public IEnumerable<DesignDiagnostic> 解析する(AnalysisContext context)
    {
        foreach (var syntaxTree in context.Project.構文木群)
        {
            var semanticModel = context.Project.意味モデルを取得する(syntaxTree);
            var root = syntaxTree.GetRoot();

            foreach (var declaration in root.DescendantNodes().OfType<ClassDeclarationSyntax>())
            {
                if (
                    semanticModel.GetDeclaredSymbol(declaration) is not INamedTypeSymbol symbol
                    || !SymbolUtilities.IsPrimaryDeclaration(symbol, declaration)
                    || symbol.BaseType
                        is not { SpecialType: not SpecialType.System_Object } baseType
                    || !baseType.Locations.Any(location => location.IsInSource)
                )
                {
                    continue;
                }

                var hiddenMembers = declaration
                    .Members.Where(member => member.Modifiers.Any(SyntaxKind.NewKeyword))
                    .Select(member => semanticModel.GetDeclaredSymbol(member))
                    .Where(member => member is not null && IsRelevantInstanceSurface(member))
                    .Where(member => HidesMatchingProjectMember(member!, baseType))
                    .ToArray();
                if (hiddenMembers.Length < MinimumHiddenMembers)
                {
                    continue;
                }

                yield return 診断生成器.生成する(
                    規則情報,
                    declaration.Identifier.GetLocation(),
                    $"This child type replaces {hiddenMembers.Length} inherited instance members from the project-owned {baseType.Name} hierarchy. Repeatedly replacing the parent-visible surface suggests the inheritance relationship may not model a stable is-a relationship.",
                    symbol.ToDisplayString()
                );
            }
        }
    }

    private static bool IsRelevantInstanceSurface(ISymbol member) =>
        member switch
        {
            IMethodSymbol method => !method.IsStatic && method.MethodKind == MethodKind.Ordinary,
            IPropertySymbol property => !property.IsStatic,
            IEventSymbol @event => !@event.IsStatic,
            _ => false,
        };

    private static bool HidesMatchingProjectMember(ISymbol member, INamedTypeSymbol baseType)
    {
        for (
            var current = baseType;
            current is not null && current.Locations.Any(location => location.IsInSource);
            current = current.BaseType
        )
        {
            if (
                current
                    .GetMembers(member.Name)
                    .Any(candidate => HasMatchingShape(member, candidate))
            )
            {
                return true;
            }
        }

        return false;
    }

    private static bool HasMatchingShape(ISymbol childMember, ISymbol baseMember) =>
        (childMember, baseMember) switch
        {
            (IMethodSymbol child, IMethodSymbol parent) => !parent.IsStatic
                && parent.MethodKind == MethodKind.Ordinary
                && child.Arity == parent.Arity
                && ParametersMatch(child.Parameters, parent.Parameters),
            (IPropertySymbol child, IPropertySymbol parent) => !parent.IsStatic
                && child.IsIndexer == parent.IsIndexer
                && ParametersMatch(child.Parameters, parent.Parameters),
            (IEventSymbol, IEventSymbol parent) => !parent.IsStatic,
            _ => false,
        };

    private static bool ParametersMatch(
        ImmutableArray<IParameterSymbol> childParameters,
        ImmutableArray<IParameterSymbol> parentParameters
    )
    {
        if (childParameters.Length != parentParameters.Length)
        {
            return false;
        }

        for (var index = 0; index < childParameters.Length; index++)
        {
            if (
                childParameters[index].RefKind != parentParameters[index].RefKind
                || !SymbolEqualityComparer.Default.Equals(
                    childParameters[index].Type,
                    parentParameters[index].Type
                )
            )
            {
                return false;
            }
        }

        return true;
    }
}
