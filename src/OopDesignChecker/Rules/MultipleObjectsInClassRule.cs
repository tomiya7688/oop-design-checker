using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using OopDesignChecker.Analysis;
using OopDesignChecker.Core;

namespace OopDesignChecker.Rules;

internal sealed class MultipleObjectsInClassRule : IAnalysisRule
{
    private const int MinimumStateMembersPerCluster = 3;
    private const int MinimumMethodsPerCluster = 3;

    public RuleDescriptor Descriptor { get; } =
        new("OOP401", "Possible multiple objects in one class", DesignDiagnosticSeverity.Warning);

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
                    || !SymbolUtilities.IsPrimaryDeclaration(symbol, declaration)
                )
                {
                    continue;
                }

                var methodUsage = BuildMethodUsage(symbol, declaration, semanticModel);
                var stateUsage = BuildEffectiveStateUsage(methodUsage);
                var clusters = FindIndependentClusters(stateUsage);
                if (clusters.Length < 2)
                {
                    continue;
                }

                yield return DiagnosticFactory.Create(
                    Descriptor,
                    declaration.Identifier.GetLocation(),
                    $"This class contains {clusters.Length} independent state/behavior clusters. The clusters do not share instance state and each has multiple state members and operations, which suggests more than one object may be represented here.",
                    symbol.ToDisplayString()
                );
            }
        }
    }

    private static Dictionary<IMethodSymbol, MethodUsage> BuildMethodUsage(
        INamedTypeSymbol containingType,
        ClassDeclarationSyntax declaration,
        SemanticModel semanticModel
    )
    {
        var result = new Dictionary<IMethodSymbol, MethodUsage>(SymbolEqualityComparer.Default);

        foreach (var methodDeclaration in declaration.Members.OfType<MethodDeclarationSyntax>())
        {
            if (
                semanticModel.GetDeclaredSymbol(methodDeclaration) is not IMethodSymbol method
                || method.IsStatic
                || method.MethodKind != MethodKind.Ordinary
            )
            {
                continue;
            }

            var stateMembers = new HashSet<ISymbol>(SymbolEqualityComparer.Default);
            foreach (
                var identifier in methodDeclaration.DescendantNodes().OfType<IdentifierNameSyntax>()
            )
            {
                var referencedSymbol = semanticModel.GetSymbolInfo(identifier).Symbol;
                if (IsInstanceStateMember(referencedSymbol, containingType))
                {
                    stateMembers.Add(referencedSymbol!);
                }
            }

            var calledMethods = new HashSet<IMethodSymbol>(SymbolEqualityComparer.Default);
            foreach (
                var invocation in methodDeclaration
                    .DescendantNodes()
                    .OfType<InvocationExpressionSyntax>()
            )
            {
                if (semanticModel.GetSymbolInfo(invocation).Symbol is not IMethodSymbol calledMethod)
                {
                    continue;
                }

                if (
                    !calledMethod.IsStatic
                    && calledMethod.MethodKind == MethodKind.Ordinary
                    && SymbolEqualityComparer.Default.Equals(
                        calledMethod.ContainingType,
                        containingType
                    )
                )
                {
                    calledMethods.Add(calledMethod);
                }
            }

            result[method] = new MethodUsage(stateMembers, calledMethods);
        }

        return result;
    }

    private static Dictionary<IMethodSymbol, HashSet<ISymbol>> BuildEffectiveStateUsage(
        IReadOnlyDictionary<IMethodSymbol, MethodUsage> methodUsage
    )
    {
        var result = new Dictionary<IMethodSymbol, HashSet<ISymbol>>(SymbolEqualityComparer.Default);

        foreach (var method in methodUsage.Keys)
        {
            var effectiveState = new HashSet<ISymbol>(SymbolEqualityComparer.Default);
            var visited = new HashSet<IMethodSymbol>(SymbolEqualityComparer.Default);
            var queue = new Queue<IMethodSymbol>();
            queue.Enqueue(method);

            while (queue.TryDequeue(out var current))
            {
                if (!visited.Add(current) || !methodUsage.TryGetValue(current, out var usage))
                {
                    continue;
                }

                effectiveState.UnionWith(usage.StateMembers);
                foreach (var calledMethod in usage.CalledMethods)
                {
                    queue.Enqueue(calledMethod);
                }
            }

            if (effectiveState.Count > 0)
            {
                result[method] = effectiveState;
            }
        }

        return result;
    }

    private static bool IsInstanceStateMember(ISymbol? symbol, INamedTypeSymbol containingType) =>
        symbol switch
        {
            IFieldSymbol field =>
                !field.IsStatic
                && SymbolEqualityComparer.Default.Equals(field.ContainingType, containingType),
            IPropertySymbol property =>
                !property.IsStatic
                && SymbolEqualityComparer.Default.Equals(property.ContainingType, containingType),
            _ => false,
        };

    private static StateCluster[] FindIndependentClusters(
        IReadOnlyDictionary<IMethodSymbol, HashSet<ISymbol>> stateUsage
    )
    {
        var allStateMembers = new HashSet<ISymbol>(
            stateUsage.Values.SelectMany(stateMembers => stateMembers),
            SymbolEqualityComparer.Default
        );
        var remaining = new HashSet<ISymbol>(allStateMembers, SymbolEqualityComparer.Default);
        var clusters = new List<StateCluster>();

        while (remaining.Count > 0)
        {
            var first = remaining.First();
            var stateMembers = ExpandConnectedState(first, stateUsage, remaining);
            var methods = new HashSet<IMethodSymbol>(
                stateUsage
                    .Where(pair => pair.Value.Any(stateMembers.Contains))
                    .Select(pair => pair.Key),
                SymbolEqualityComparer.Default
            );

            if (
                stateMembers.Count >= MinimumStateMembersPerCluster
                && methods.Count >= MinimumMethodsPerCluster
            )
            {
                clusters.Add(new StateCluster(stateMembers.Count, methods.Count));
            }
        }

        return clusters.ToArray();
    }

    private static HashSet<ISymbol> ExpandConnectedState(
        ISymbol first,
        IReadOnlyDictionary<IMethodSymbol, HashSet<ISymbol>> stateUsage,
        HashSet<ISymbol> remaining
    )
    {
        var result = new HashSet<ISymbol>(SymbolEqualityComparer.Default);
        var queue = new Queue<ISymbol>();
        queue.Enqueue(first);
        remaining.Remove(first);

        while (queue.TryDequeue(out var current))
        {
            result.Add(current);
            foreach (var stateMembers in stateUsage.Values.Where(items => items.Contains(current)))
            {
                foreach (var related in stateMembers)
                {
                    if (remaining.Remove(related))
                    {
                        queue.Enqueue(related);
                    }
                }
            }
        }

        return result;
    }

    private sealed record MethodUsage(
        HashSet<ISymbol> StateMembers,
        HashSet<IMethodSymbol> CalledMethods
    );

    private readonly record struct StateCluster(int StateMemberCount, int MethodCount);
}
