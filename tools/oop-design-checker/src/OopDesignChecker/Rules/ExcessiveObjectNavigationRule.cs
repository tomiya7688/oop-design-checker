using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using OopDesignChecker.Analysis;
using OopDesignChecker.Core;

namespace OopDesignChecker.Rules;

internal sealed class ExcessiveObjectNavigationRule : I解析規則
{
    private const int MinimumNavigationDepth = 3;

    public RuleDescriptor 規則情報 { get; } =
        new(
            "OOP404",
            "Excessive navigation through object internals",
            DesignDiagnosticSeverity.Attention
        );

    public IEnumerable<DesignDiagnostic> 解析する(解析文脈 context)
    {
        foreach (var syntaxTree in context.対象プロジェクト.構文木群)
        {
            var semanticModel = context.対象プロジェクト.意味モデルを取得する(syntaxTree);
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

                yield return 診断生成器.生成する(
                    規則情報,
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
                    データ保持型判定器.明示的データ保持型である(containingType)
                    || データ保持型判定器.明示的データ保持型である(memberType)
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
