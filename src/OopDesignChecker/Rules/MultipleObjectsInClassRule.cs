using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using OopDesignChecker.Analysis;
using OopDesignChecker.Core;

namespace OopDesignChecker.Rules;

internal sealed class MultipleObjectsInClassRule : IAnalysisRule
{
    private const int MinimumFieldsPerCluster = 3;
    private const int MinimumMethodsPerCluster = 3;

    public RuleDescriptor Descriptor { get; } =
        new(
            "OOP401",
            "Possible multiple objects in one class",
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
                    || !SymbolUtilities.IsPrimaryDeclaration(symbol, declaration))
                {
                    continue;
                }

                var fieldUsage = BuildFieldUsage(symbol, declaration, semanticModel);
                var clusters = FindIndependentClusters(fieldUsage);
                if (clusters.Length < 2)
                {
                    continue;
                }

                yield return DiagnosticFactory.Create(
                    Descriptor,
                    declaration.Identifier.GetLocation(),
                    $"This class contains {clusters.Length} independent state/behavior clusters. The clusters do not share instance state and each has multiple fields and operations, which suggests more than one object may be represented here.",
                    symbol.ToDisplayString()
                );
            }
        }
    }

    private static IReadOnlyDictionary<IMethodSymbol, HashSet<IFieldSymbol>> BuildFieldUsage(
        INamedTypeSymbol containingType,
        ClassDeclarationSyntax declaration,
        SemanticModel semanticModel
    )
    {
        var result = new Dictionary<IMethodSymbol, HashSet<IFieldSymbol>>(SymbolEqualityComparer.Default);

        foreach (var methodDeclaration in declaration.Members.OfType<MethodDeclarationSyntax>())
        {
            if (semanticModel.GetDeclaredSymbol(methodDeclaration) is not IMethodSymbol method
                || method.IsStatic
                || method.MethodKind != MethodKind.Ordinary)
            {
                continue;
            }

            var fields = methodDeclaration.DescendantNodes()
                .OfType<IdentifierNameSyntax>()
                .Select(identifier => semanticModel.GetSymbolInfo(identifier).Symbol)
                .OfType<IFieldSymbol>()
                .Where(field =>
                    !field.IsStatic
                    && SymbolEqualityComparer.Default.Equals(field.ContainingType, containingType)
                )
                .ToHashSet(SymbolEqualityComparer.Default);

            if (fields.Count > 0)
            {
                result[method] = fields;
            }
        }

        return result;
    }

    private static FieldCluster[] FindIndependentClusters(
        IReadOnlyDictionary<IMethodSymbol, HashSet<IFieldSymbol>> fieldUsage
    )
    {
        var allFields = fieldUsage.Values.SelectMany(fields => fields).ToHashSet(SymbolEqualityComparer.Default);
        var remaining = new HashSet<IFieldSymbol>(allFields, SymbolEqualityComparer.Default);
        var clusters = new List<FieldCluster>();

        while (remaining.Count > 0)
        {
            var first = remaining.First();
            var fields = ExpandConnectedFields(first, fieldUsage, remaining);
            var methods = fieldUsage
                .Where(pair => pair.Value.Any(fields.Contains))
                .Select(pair => pair.Key)
                .ToHashSet(SymbolEqualityComparer.Default);

            if (fields.Count >= MinimumFieldsPerCluster && methods.Count >= MinimumMethodsPerCluster)
            {
                clusters.Add(new FieldCluster(fields.Count, methods.Count));
            }
        }

        return clusters.ToArray();
    }

    private static HashSet<IFieldSymbol> ExpandConnectedFields(
        IFieldSymbol first,
        IReadOnlyDictionary<IMethodSymbol, HashSet<IFieldSymbol>> fieldUsage,
        HashSet<IFieldSymbol> remaining
    )
    {
        var result = new HashSet<IFieldSymbol>(SymbolEqualityComparer.Default);
        var queue = new Queue<IFieldSymbol>();
        queue.Enqueue(first);
        remaining.Remove(first);

        while (queue.TryDequeue(out var current))
        {
            result.Add(current);
            foreach (var fields in fieldUsage.Values.Where(fields => fields.Contains(current)))
            {
                foreach (var related in fields)
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

    private readonly record struct FieldCluster(int FieldCount, int MethodCount);
}
