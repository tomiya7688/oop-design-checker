using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using OopDesignChecker.Analysis;
using OopDesignChecker.Core;

namespace OopDesignChecker.Rules;

internal sealed class SuspiciousInheritanceRelationshipRule : IAnalysisRule
{
    private const int MinimumHiddenMembers = 2;

    public RuleDescriptor Descriptor { get; } =
        new(
            "OOP301",
            "Suspicious inheritance relationship",
            DesignDiagnosticSeverity.Warning
        );

    public IEnumerable<DesignDiagnostic> Analyze(AnalysisContext context)
    {
        foreach (var syntaxTree in context.Project.SyntaxTrees)
        {
            var semanticModel = context.Project.GetSemanticModel(syntaxTree);
            var root = syntaxTree.GetRoot();

            foreach (var declaration in root.DescendantNodes().OfType<ClassDeclarationSyntax>())
            {
                if (semanticModel.GetDeclaredSymbol(declaration) is not INamedTypeSymbol symbol
                    || symbol.BaseType is not { SpecialType: not SpecialType.System_Object } baseType)
                {
                    continue;
                }

                var hiddenMembers = declaration.Members
                    .Where(member => member.Modifiers.Any(SyntaxKind.NewKeyword))
                    .Select(member => semanticModel.GetDeclaredSymbol(member))
                    .Where(member => member is not null && HidesBaseMember(member, baseType))
                    .ToArray();
                if (hiddenMembers.Length < MinimumHiddenMembers)
                {
                    continue;
                }

                yield return DiagnosticFactory.Create(
                    Descriptor,
                    declaration.Identifier.GetLocation(),
                    $"This child type hides {hiddenMembers.Length} inherited members from {baseType.Name}. Repeatedly replacing the visible parent surface suggests the inheritance relationship may not model a stable is-a relationship.",
                    symbol.ToDisplayString()
                );
            }
        }
    }

    private static bool HidesBaseMember(ISymbol member, INamedTypeSymbol baseType)
    {
        for (var current = baseType; current is not null; current = current.BaseType)
        {
            if (current.GetMembers(member.Name).Length > 0)
            {
                return true;
            }
        }

        return false;
    }
}
