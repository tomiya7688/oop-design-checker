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

            foreach (var creation in root.DescendantNodes().OfType<ObjectCreationExpressionSyntax>())
            {
                if (semanticModel.GetTypeInfo(creation).Type is not INamedTypeSymbol concreteType
                    || concreteType.TypeKind != TypeKind.Class
                    || !IsReplaceableProjectService(concreteType)
                    || IsOwnedConstruction(creation))
                {
                    continue;
                }

                var containingType = semanticModel.GetEnclosingSymbol(creation.SpanStart)?.ContainingType;
                if (containingType is null
                    || SymbolEqualityComparer.Default.Equals(containingType, concreteType))
                {
                    continue;
                }

                var abstraction = concreteType.AllInterfaces.First(IsSourceDefinedInterface);
                yield return DiagnosticFactory.Create(
                    Descriptor,
                    creation.NewKeyword.GetLocation(),
                    $"{containingType.Name} directly constructs replaceable collaborator {concreteType.Name}, which implements project abstraction {abstraction.Name}. Prefer receiving the abstraction when the collaborator lifecycle is not owned here.",
                    containingType.ToDisplayString()
                );
            }
        }
    }

    private static bool IsReplaceableProjectService(INamedTypeSymbol type) =>
        type.AllInterfaces.Any(IsSourceDefinedInterface)
        && type.GetMembers().OfType<IMethodSymbol>().Any(method => method.MethodKind == MethodKind.Ordinary);

    private static bool IsSourceDefinedInterface(INamedTypeSymbol interfaceType) =>
        interfaceType.TypeKind == TypeKind.Interface
        && interfaceType.Locations.Any(location => location.IsInSource);

    private static bool IsOwnedConstruction(ObjectCreationExpressionSyntax creation) =>
        creation.Ancestors().Any(ancestor =>
            ancestor is ReturnStatementSyntax
                or ArrowExpressionClauseSyntax
                or ObjectCreationExpressionSyntax
                or CollectionExpressionSyntax
        );
}
