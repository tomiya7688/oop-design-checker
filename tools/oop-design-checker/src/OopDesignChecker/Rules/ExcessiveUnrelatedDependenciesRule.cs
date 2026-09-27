using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using OopDesignChecker.Analysis;
using OopDesignChecker.Core;

namespace OopDesignChecker.Rules;

internal sealed class ExcessiveUnrelatedDependenciesRule : IAnalysisRule
{
    private const int MinimumClusters = 3;
    private const int MinimumDependenciesPerCluster = 2;

    public RuleDescriptor Descriptor { get; } =
        new("OOP403", "Excessive unrelated dependencies", DesignDiagnosticSeverity.Warning);

    public IEnumerable<DesignDiagnostic> Analyze(AnalysisContext context)
    {
        foreach (var syntaxTree in context.Project.SyntaxTrees)
        {
            var semanticModel = context.Project.GetSemanticModel(syntaxTree);
            var root = syntaxTree.GetRoot();

            foreach (var declaration in root.DescendantNodes().OfType<ClassDeclarationSyntax>())
            {
                if (semanticModel.GetDeclaredSymbol(declaration) is not INamedTypeSymbol symbol)
                {
                    continue;
                }

                var dependencies = ReadDependencyFields(symbol);
                if (dependencies.Length < MinimumClusters * MinimumDependenciesPerCluster)
                {
                    continue;
                }

                var methodUsage = ReadMethodUsage(symbol, declaration, dependencies, semanticModel);
                var effectiveUsage = BuildEffectiveDependencyUsage(methodUsage);
                var clusters = CountDependencyClusters(dependencies, effectiveUsage);
                if (clusters < MinimumClusters)
                {
                    continue;
                }

                yield return DiagnosticFactory.Create(
                    Descriptor,
                    declaration.Identifier.GetLocation(),
                    $"This object contains {clusters} independent groups of project dependencies. Distinct dependency clusters often indicate several separate object concerns are being coordinated inside one object.",
                    symbol.ToDisplayString()
                );
            }
        }
    }

    private static IFieldSymbol[] ReadDependencyFields(INamedTypeSymbol type) =>
        type.GetMembers()
            .OfType<IFieldSymbol>()
            .Where(field =>
                !field.IsStatic
                && field.Type is INamedTypeSymbol namedType
                && namedType.SpecialType == SpecialType.None
                && namedType.Locations.Any(location => location.IsInSource)
            )
            .ToArray();

    private static Dictionary<IMethodSymbol, MethodUsage> ReadMethodUsage(
        INamedTypeSymbol containingType,
        ClassDeclarationSyntax declaration,
        IReadOnlyCollection<IFieldSymbol> dependencies,
        SemanticModel semanticModel
    )
    {
        var dependencySet = new HashSet<IFieldSymbol>(dependencies, SymbolEqualityComparer.Default);
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

            var usedDependencies = new HashSet<IFieldSymbol>(
                methodDeclaration
                    .DescendantNodes()
                    .OfType<IdentifierNameSyntax>()
                    .Select(identifier => semanticModel.GetSymbolInfo(identifier).Symbol)
                    .OfType<IFieldSymbol>()
                    .Where(dependencySet.Contains),
                SymbolEqualityComparer.Default
            );

            var calledMethods = new HashSet<IMethodSymbol>(SymbolEqualityComparer.Default);
            foreach (
                var invocation in methodDeclaration
                    .DescendantNodes()
                    .OfType<InvocationExpressionSyntax>()
            )
            {
                if (
                    semanticModel.GetSymbolInfo(invocation).Symbol is IMethodSymbol calledMethod
                    && !calledMethod.IsStatic
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

            result[method] = new MethodUsage(usedDependencies, calledMethods);
        }

        return result;
    }

    private static Dictionary<IMethodSymbol, HashSet<IFieldSymbol>> BuildEffectiveDependencyUsage(
        IReadOnlyDictionary<IMethodSymbol, MethodUsage> methodUsage
    )
    {
        var result = new Dictionary<IMethodSymbol, HashSet<IFieldSymbol>>(
            SymbolEqualityComparer.Default
        );

        foreach (var method in methodUsage.Keys)
        {
            var dependencies = new HashSet<IFieldSymbol>(SymbolEqualityComparer.Default);
            var visited = new HashSet<IMethodSymbol>(SymbolEqualityComparer.Default);
            var queue = new Queue<IMethodSymbol>();
            queue.Enqueue(method);

            while (queue.TryDequeue(out var current))
            {
                if (!visited.Add(current) || !methodUsage.TryGetValue(current, out var usage))
                {
                    continue;
                }

                dependencies.UnionWith(usage.Dependencies);
                foreach (var calledMethod in usage.CalledMethods)
                {
                    queue.Enqueue(calledMethod);
                }
            }

            if (dependencies.Count > 0)
            {
                result[method] = dependencies;
            }
        }

        return result;
    }

    private static int CountDependencyClusters(
        IReadOnlyCollection<IFieldSymbol> dependencies,
        IReadOnlyDictionary<IMethodSymbol, HashSet<IFieldSymbol>> usage
    )
    {
        var remaining = new HashSet<IFieldSymbol>(dependencies, SymbolEqualityComparer.Default);
        var qualifyingClusters = 0;

        while (remaining.Count > 0)
        {
            var queue = new Queue<IFieldSymbol>();
            var cluster = new HashSet<IFieldSymbol>(SymbolEqualityComparer.Default);
            var first = remaining.First();
            remaining.Remove(first);
            queue.Enqueue(first);

            while (queue.TryDequeue(out var current))
            {
                cluster.Add(current);
                ConnectByMethodUsage(current, usage, remaining, queue);
                ConnectBySharedAbstraction(current, remaining, queue);
            }

            if (
                cluster.Count >= MinimumDependenciesPerCluster
                && usage.Values.Any(fields => fields.Any(cluster.Contains))
            )
            {
                qualifyingClusters++;
            }
        }

        return qualifyingClusters;
    }

    private static void ConnectByMethodUsage(
        IFieldSymbol current,
        IReadOnlyDictionary<IMethodSymbol, HashSet<IFieldSymbol>> usage,
        HashSet<IFieldSymbol> remaining,
        Queue<IFieldSymbol> queue
    )
    {
        foreach (var methodFields in usage.Values.Where(fields => fields.Contains(current)))
        {
            foreach (var related in methodFields)
            {
                if (remaining.Remove(related))
                {
                    queue.Enqueue(related);
                }
            }
        }
    }

    private static void ConnectBySharedAbstraction(
        IFieldSymbol current,
        HashSet<IFieldSymbol> remaining,
        Queue<IFieldSymbol> queue
    )
    {
        var relatedFields = remaining
            .Where(candidate => ShareMeaningfulProjectAbstraction(current.Type, candidate.Type))
            .ToArray();

        foreach (var related in relatedFields)
        {
            if (remaining.Remove(related))
            {
                queue.Enqueue(related);
            }
        }
    }

    private static bool ShareMeaningfulProjectAbstraction(ITypeSymbol left, ITypeSymbol right)
    {
        if (left is not INamedTypeSymbol leftType || right is not INamedTypeSymbol rightType)
        {
            return false;
        }

        var leftHierarchy = ReadProjectTypeHierarchy(leftType);
        return ReadProjectTypeHierarchy(rightType).Any(leftHierarchy.Contains);
    }

    private static HashSet<INamedTypeSymbol> ReadProjectTypeHierarchy(INamedTypeSymbol type)
    {
        var result = new HashSet<INamedTypeSymbol>(SymbolEqualityComparer.Default);

        for (
            INamedTypeSymbol? current = type;
            current is not null && current.SpecialType != SpecialType.System_Object;
            current = current.BaseType
        )
        {
            if (current.Locations.Any(location => location.IsInSource))
            {
                result.Add(current);
            }
        }

        foreach (
            var abstraction in type.AllInterfaces.Where(candidate =>
                candidate.Locations.Any(location => location.IsInSource)
            )
        )
        {
            result.Add(abstraction);
        }

        return result;
    }

    private sealed record MethodUsage(
        HashSet<IFieldSymbol> Dependencies,
        HashSet<IMethodSymbol> CalledMethods
    );
}
