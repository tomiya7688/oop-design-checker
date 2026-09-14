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
            if (related.Length < MinimumRelatedTypes)
            {
                continue;
            }

            var names = string.Join(", ", related.Select(candidate => candidate.Symbol.Name));
            yield return DiagnosticFactory.Create(
                Descriptor,
                related[0].Declaration.Identifier.GetLocation(),
                $"Types {names} expose the same {related[0].OperationCount} public instance operations but share no project abstraction. Consider an interface or meaningful base abstraction if callers treat them as the same concept."
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
