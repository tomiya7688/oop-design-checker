using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using OopDesignChecker.Analysis;
using OopDesignChecker.Core;

namespace OopDesignChecker.Rules;

internal sealed class AvoidableConcreteConstructionRule : IAnalysisRule
{
    public RuleDescriptor Descriptor { get; } =
        new(
            "OOP306",
            "Avoidable concrete construction dependency",
            DesignDiagnosticSeverity.Warning
        );

    public IEnumerable<DesignDiagnostic> Analyze(AnalysisContext context)
    {
        foreach (var syntaxTree in context.Project.SyntaxTrees)
        {
            var semanticModel = context.Project.GetSemanticModel(syntaxTree);
            var root = syntaxTree.GetRoot();

            foreach (
                var creation in root.DescendantNodes().OfType<ObjectCreationExpressionSyntax>()
            )
            {
                if (
                    semanticModel.GetTypeInfo(creation).Type is not INamedTypeSymbol concreteType
                    || concreteType.TypeKind != TypeKind.Class
                    || IsOwnedConstruction(creation)
                )
                {
                    continue;
                }

                var abstraction = ProjectAbstractionClassifier.FindMeaningfulAbstraction(
                    concreteType
                );
                if (abstraction is null || !HasBehavior(concreteType))
                {
                    continue;
                }

                var containingType = semanticModel
                    .GetEnclosingSymbol(creation.SpanStart)
                    ?.ContainingType;
                if (
                    containingType is null
                    || SymbolEqualityComparer.Default.Equals(containingType, concreteType)
                )
                {
                    continue;
                }

                yield return DiagnosticFactory.Create(
                    Descriptor,
                    creation.NewKeyword.GetLocation(),
                    $"{containingType.Name} directly constructs replaceable collaborator {concreteType.Name}, while project abstraction {abstraction.Name} defines a behavioral contract. Prefer receiving the abstraction when the collaborator lifecycle is not owned here.",
                    containingType.ToDisplayString()
                );
            }
        }
    }

    private static bool HasBehavior(INamedTypeSymbol type) =>
        type.GetMembers()
            .OfType<IMethodSymbol>()
            .Any(method => method.MethodKind == MethodKind.Ordinary && !method.IsStatic);

    private static bool IsOwnedConstruction(ObjectCreationExpressionSyntax creation) =>
        creation
            .Ancestors()
            .Any(ancestor =>
                ancestor
                    is ReturnStatementSyntax
                        or ArrowExpressionClauseSyntax
                        or ObjectCreationExpressionSyntax
                        or CollectionExpressionSyntax
            );
}
