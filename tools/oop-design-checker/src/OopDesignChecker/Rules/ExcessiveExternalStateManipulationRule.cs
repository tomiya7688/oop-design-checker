using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using OopDesignChecker.Analysis;
using OopDesignChecker.Core;

namespace OopDesignChecker.Rules;

internal sealed class ExcessiveExternalStateManipulationRule : IAnalysisRule
{
    private const int MinimumDistinctMembers = 3;

    public RuleDescriptor Descriptor { get; } =
        new("OOP108", "Excessive external state manipulation", DesignDiagnosticSeverity.Warning);

    public IEnumerable<DesignDiagnostic> Analyze(AnalysisContext context)
    {
        foreach (var syntaxTree in context.Project.SyntaxTrees)
        {
            var semanticModel = context.Project.GetSemanticModel(syntaxTree);
            var root = syntaxTree.GetRoot();

            foreach (
                var methodDeclaration in root.DescendantNodes().OfType<MethodDeclarationSyntax>()
            )
            {
                if (semanticModel.GetDeclaredSymbol(methodDeclaration) is not IMethodSymbol method)
                {
                    continue;
                }

                var manipulatedGroups = methodDeclaration
                    .DescendantNodes()
                    .OfType<AssignmentExpressionSyntax>()
                    .Select(assignment => ReadExternalWrite(assignment, method, semanticModel))
                    .Where(write => write is not null)
                    .Select(write => write!.Value)
                    .GroupBy(
                        write => (write.Receiver, write.TargetType),
                        ExternalWriteKeyComparer.Instance
                    );

                foreach (var group in manipulatedGroups)
                {
                    var distinctMembers = group
                        .Select(write => write.MemberName)
                        .Distinct(StringComparer.Ordinal)
                        .ToArray();
                    if (distinctMembers.Length < MinimumDistinctMembers)
                    {
                        continue;
                    }

                    yield return DiagnosticFactory.Create(
                        Descriptor,
                        methodDeclaration.Identifier.GetLocation(),
                        $"This method directly writes {distinctMembers.Length} members of {group.Key.TargetType.Name} through {group.Key.Receiver}. Prefer asking that object to perform the state transition itself.",
                        method.ToDisplayString()
                    );
                }
            }
        }
    }

    private static ExternalWrite? ReadExternalWrite(
        AssignmentExpressionSyntax assignment,
        IMethodSymbol method,
        SemanticModel semanticModel
    )
    {
        if (assignment.Left is not MemberAccessExpressionSyntax memberAccess)
        {
            return null;
        }

        var member = semanticModel.GetSymbolInfo(memberAccess).Symbol;
        var targetType = member?.ContainingType;
        if (
            targetType is null
            || !targetType.Locations.Any(location => location.IsInSource)
            || SymbolEqualityComparer.Default.Equals(targetType, method.ContainingType)
            || DataCarrierClassifier.IsExplicitDataCarrier(targetType)
            || !IsPublicInstanceState(member)
        )
        {
            return null;
        }

        return new ExternalWrite(memberAccess.Expression.ToString(), targetType, member!.Name);
    }

    private static bool IsPublicInstanceState(ISymbol? symbol) =>
        symbol
            is IPropertySymbol
                {
                    IsStatic: false,
                    SetMethod.DeclaredAccessibility: Accessibility.Public
                }
                or IFieldSymbol { IsStatic: false, DeclaredAccessibility: Accessibility.Public };

    private readonly record struct ExternalWrite(
        string Receiver,
        INamedTypeSymbol TargetType,
        string MemberName
    );

    private sealed class ExternalWriteKeyComparer
        : IEqualityComparer<(string Receiver, INamedTypeSymbol TargetType)>
    {
        public static ExternalWriteKeyComparer Instance { get; } = new();

        public bool Equals(
            (string Receiver, INamedTypeSymbol TargetType) left,
            (string Receiver, INamedTypeSymbol TargetType) right
        ) =>
            StringComparer.Ordinal.Equals(left.Receiver, right.Receiver)
            && SymbolEqualityComparer.Default.Equals(left.TargetType, right.TargetType);

        public int GetHashCode((string Receiver, INamedTypeSymbol TargetType) value) =>
            HashCode.Combine(
                StringComparer.Ordinal.GetHashCode(value.Receiver),
                SymbolEqualityComparer.Default.GetHashCode(value.TargetType)
            );
    }
}
