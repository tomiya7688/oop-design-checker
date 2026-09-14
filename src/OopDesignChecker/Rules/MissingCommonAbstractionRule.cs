using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using OopDesignChecker.Analysis;
using OopDesignChecker.Core;

namespace OopDesignChecker.Rules;

internal sealed class MissingCommonAbstractionRule : IAnalysisRule
{
    private const int MinimumRelatedTypes = 3;
    private const int MinimumSharedOperations = 2;

    public RuleDescriptor Descriptor { get; } =
        new("OOP001", "Missing common abstraction", DesignDiagnosticSeverity.Warning);

    public IEnumerable<DesignDiagnostic> Analyze(AnalysisContext context)
    {
        var candidates = context
            .Project.SyntaxTrees.SelectMany(tree => FindCandidates(context, tree))
            .ToArray();

        foreach (
            var group in candidates.GroupBy(
                candidate => candidate.SignatureKey,
                StringComparer.Ordinal
            )
        )
        {
            var related = group.ToArray();
            if (
                related.Length < MinimumRelatedTypes
                || !HasSharedUsageContext(context, related)
            )
            {
                continue;
            }

            var names = string.Join(", ", related.Select(candidate => candidate.Symbol.Name));
            yield return DiagnosticFactory.Create(
                Descriptor,
                related[0].Declaration.Identifier.GetLocation(),
                $"Types {names} expose the same {related[0].OperationCount} public instance operations and are used in the same call-site role, but share no project abstraction. Consider an interface or meaningful base abstraction."
            );
        }
    }

    private static IEnumerable<AbstractionCandidate> FindCandidates(
        AnalysisContext context,
        SyntaxTree syntaxTree
    )
    {
        var semanticModel = context.Project.GetSemanticModel(syntaxTree);
        var root = syntaxTree.GetRoot();

        foreach (var declaration in root.DescendantNodes().OfType<ClassDeclarationSyntax>())
        {
            if (
                semanticModel.GetDeclaredSymbol(declaration) is not INamedTypeSymbol symbol
                || !SymbolUtilities.IsPrimaryDeclaration(symbol, declaration)
                || HasProjectAbstraction(symbol)
            )
            {
                continue;
            }

            var signatures = symbol
                .GetMembers()
                .OfType<IMethodSymbol>()
                .Where(method =>
                    method.MethodKind == MethodKind.Ordinary
                    && !method.IsStatic
                    && method.DeclaredAccessibility == Accessibility.Public
                )
                .Select(CreateSignature)
                .OrderBy(signature => signature, StringComparer.Ordinal)
                .ToArray();

            if (signatures.Length < MinimumSharedOperations)
            {
                continue;
            }

            yield return new AbstractionCandidate(
                declaration,
                symbol,
                string.Join("|", signatures),
                signatures.Length
            );
        }
    }

    private static bool HasSharedUsageContext(
        AnalysisContext context,
        IReadOnlyList<AbstractionCandidate> related
    )
    {
        var methods = new List<IMethodSymbol>();

        foreach (var syntaxTree in context.Project.SyntaxTrees)
        {
            var semanticModel = context.Project.GetSemanticModel(syntaxTree);
            foreach (
                var declaration in syntaxTree
                    .GetRoot()
                    .DescendantNodes()
                    .OfType<MethodDeclarationSyntax>()
            )
            {
                if (semanticModel.GetDeclaredSymbol(declaration) is IMethodSymbol method)
                {
                    methods.Add(method);
                }
            }
        }

        foreach (
            var overloadGroup in methods.GroupBy(method =>
                $"{method.ContainingType.ToDisplayString()}|{method.Name}|{method.Parameters.Length}"
            )
        )
        {
            var overloads = overloadGroup.ToArray();
            if (overloads.Length < MinimumRelatedTypes || overloads[0].Parameters.Length == 0)
            {
                continue;
            }

            for (var index = 0; index < overloads[0].Parameters.Length; index++)
            {
                var matchedTypes = new List<INamedTypeSymbol>();
                foreach (var overload in overloads)
                {
                    if (
                        overload.Parameters[index].Type is not INamedTypeSymbol parameterType
                        || !TryMatchRelated(parameterType, related, out var matched)
                        || matchedTypes.Any(existing =>
                            SymbolEqualityComparer.Default.Equals(existing, matched)
                        )
                    )
                    {
                        continue;
                    }

                    matchedTypes.Add(matched);
                }

                if (matchedTypes.Count >= MinimumRelatedTypes)
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static bool TryMatchRelated(
        INamedTypeSymbol type,
        IReadOnlyList<AbstractionCandidate> related,
        out INamedTypeSymbol matched
    )
    {
        foreach (var candidate in related)
        {
            if (SymbolEqualityComparer.Default.Equals(type, candidate.Symbol))
            {
                matched = candidate.Symbol;
                return true;
            }
        }

        matched = null!;
        return false;
    }

    private static bool HasProjectAbstraction(INamedTypeSymbol symbol) =>
        symbol.BaseType is { SpecialType: not SpecialType.System_Object }
        || symbol.AllInterfaces.Any(interfaceType =>
            interfaceType.Locations.Any(location => location.IsInSource)
        );

    private static string CreateSignature(IMethodSymbol method)
    {
        var parameters = string.Join(
            ",",
            method.Parameters.Select(parameter => parameter.Type.ToDisplayString())
        );
        return $"{method.Name}({parameters}):{method.ReturnType.ToDisplayString()}";
    }

    private sealed record AbstractionCandidate(
        ClassDeclarationSyntax Declaration,
        INamedTypeSymbol Symbol,
        string SignatureKey,
        int OperationCount
    );
}
