using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using OopDesignChecker.Analysis;
using OopDesignChecker.Core;

namespace OopDesignChecker.Rules;

internal sealed class ExcessiveObjectNavigationRule : IAnalysisRule
{
    private const int MinimumNavigationDepth = 3;

    public RuleDescriptor Descriptor { get; } =
        new(
            "OOP404",
            "Excessive navigation through object internals",
            DesignDiagnosticSeverity.Attention
        );

    public IEnumerable<DesignDiagnostic> Analyze(AnalysisContext context)
    {
        foreach (var syntaxTree in context.Project.SyntaxTrees)
        {
            var semanticModel = context.Project.GetSemanticModel(syntaxTree);
            var root = syntaxTree.GetRoot();

            foreach (var invocation in root.DescendantNodes().OfType<InvocationExpressionSyntax>())
            {
                if (invocation.Expression is not MemberAccessExpressionSyntax methodAccess)
                {
                    continue;
                }

                var navigationDepth = CountProjectObjectBoundaries(
                    methodAccess.Expression,
                    semanticModel
                );
                if (navigationDepth < MinimumNavigationDepth)
                {
                    continue;
                }

                yield return DiagnosticFactory.Create(
                    Descriptor,
                    invocation.GetLocation(),
                    $"This call traverses {navigationDepth} project object boundaries before invoking behavior. Repeated deep navigation couples the caller to another object's internal graph."
                );
            }
        }
    }

    private static int CountProjectObjectBoundaries(
        ExpressionSyntax expression,
        SemanticModel semanticModel
    )
    {
        var boundaries = 0;
        var current = expression;

        while (current is MemberAccessExpressionSyntax memberAccess)
        {
            var symbol = semanticModel.GetSymbolInfo(memberAccess).Symbol;
            var memberType = ReadInstanceMemberType(symbol);
            var containingType = symbol?.ContainingType;

            if (
                containingType is not null
                && memberType is not null
                && (
                    DataCarrierClassifier.IsExplicitDataCarrier(containingType)
                    || DataCarrierClassifier.IsExplicitDataCarrier(memberType)
                )
            )
            {
                return 0;
            }

            if (
                containingType is not null
                && memberType is not null
                && IsProjectObjectType(containingType)
                && IsProjectObjectType(memberType)
                && !RepresentsSameObjectAbstraction(containingType, memberType)
            )
            {
                boundaries++;
            }

            current = memberAccess.Expression;
        }

        return boundaries;
    }

    private static INamedTypeSymbol? ReadInstanceMemberType(ISymbol? symbol) =>
        symbol switch
        {
            IPropertySymbol { IsStatic: false, Type: INamedTypeSymbol type } => type,
            IFieldSymbol { IsStatic: false, Type: INamedTypeSymbol type } => type,
            _ => null,
        };

    private static bool IsProjectObjectType(INamedTypeSymbol type) =>
        type.IsReferenceType
        && type.SpecialType == SpecialType.None
        && type.Locations.Any(location => location.IsInSource);

    private static bool RepresentsSameObjectAbstraction(
        INamedTypeSymbol containingType,
        INamedTypeSymbol memberType
    ) =>
        SymbolEqualityComparer.Default.Equals(containingType, memberType)
        || ImplementsOrDerivesFrom(containingType, memberType)
        || ImplementsOrDerivesFrom(memberType, containingType);

    private static bool ImplementsOrDerivesFrom(INamedTypeSymbol type, INamedTypeSymbol candidate)
    {
        if (type.AllInterfaces.Any(item => SymbolEqualityComparer.Default.Equals(item, candidate)))
        {
            return true;
        }

        for (var current = type.BaseType; current is not null; current = current.BaseType)
        {
            if (SymbolEqualityComparer.Default.Equals(current, candidate))
            {
                return true;
            }
        }

        return false;
    }
}
