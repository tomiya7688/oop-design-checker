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

                var usage = ReadDependencyUsage(declaration, dependencies, semanticModel);
                var clusters = CountDependencyClusters(dependencies, usage);
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

    private static Dictionary<IMethodSymbol, HashSet<IFieldSymbol>> ReadDependencyUsage(
        ClassDeclarationSyntax declaration,
        IReadOnlyCollection<IFieldSymbol> dependencies,
        SemanticModel semanticModel
    )
    {
        var dependencySet = new HashSet<IFieldSymbol>(dependencies, SymbolEqualityComparer.Default);
        var result = new Dictionary<IMethodSymbol, HashSet<IFieldSymbol>>(
            SymbolEqualityComparer.Default
        );

        foreach (var methodDeclaration in declaration.Members.OfType<MethodDeclarationSyntax>())
        {
            if (
                semanticModel.GetDeclaredSymbol(methodDeclaration) is not IMethodSymbol method
                || method.IsStatic
            )
            {
                continue;
            }

            var used = new HashSet<IFieldSymbol>(
                methodDeclaration
                    .DescendantNodes()
                    .OfType<IdentifierNameSyntax>()
                    .Select(identifier => semanticModel.GetSymbolInfo(identifier).Symbol)
                    .OfType<IFieldSymbol>()
                    .Where(dependencySet.Contains),
                SymbolEqualityComparer.Default
            );
            if (used.Count > 0)
            {
                result[method] = used;
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
}
