using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using OopDesignChecker.Analysis;
using OopDesignChecker.Core;

namespace OopDesignChecker.Rules;

internal sealed class UnnecessaryAbstractionRule : IAnalysisRule
{
    public RuleDescriptor Descriptor { get; } =
        new("OOP003", "Unnecessary abstraction candidate", DesignDiagnosticSeverity.Attention);

    public IEnumerable<DesignDiagnostic> Analyze(AnalysisContext context)
    {
        var interfaces = ReadSourceInterfaces(context).ToArray();
        var classes = ReadSourceClasses(context).ToArray();

        foreach (var interfaceEntry in interfaces)
        {
            var implementations = classes
                .Where(entry =>
                    entry.Symbol.AllInterfaces.Any(implemented =>
                        SymbolEqualityComparer.Default.Equals(implemented, interfaceEntry.Symbol)
                    )
                )
                .ToArray();
            if (
                implementations.Length != 1
                || HasTypedUseOutsideImplementation(
                    context,
                    interfaceEntry.Symbol,
                    implementations[0].Symbol
                )
            )
            {
                continue;
            }

            yield return DiagnosticFactory.Create(
                Descriptor,
                interfaceEntry.Declaration.Identifier.GetLocation(),
                $"{interfaceEntry.Symbol.Name} has one implementation and is not used as a declared type outside that implementation. The abstraction currently adds ceremony without enabling polymorphic use.",
                interfaceEntry.Symbol.ToDisplayString()
            );
        }
    }

    private static IEnumerable<InterfaceEntry> ReadSourceInterfaces(AnalysisContext context)
    {
        foreach (var syntaxTree in context.Project.SyntaxTrees)
        {
            var semanticModel = context.Project.GetSemanticModel(syntaxTree);
            foreach (
                var declaration in syntaxTree
                    .GetRoot()
                    .DescendantNodes()
                    .OfType<InterfaceDeclarationSyntax>()
            )
            {
                if (semanticModel.GetDeclaredSymbol(declaration) is INamedTypeSymbol symbol)
                {
                    yield return new InterfaceEntry(declaration, symbol);
                }
            }
        }
    }

    private static IEnumerable<ClassEntry> ReadSourceClasses(AnalysisContext context)
    {
        foreach (var syntaxTree in context.Project.SyntaxTrees)
        {
            var semanticModel = context.Project.GetSemanticModel(syntaxTree);
            foreach (
                var declaration in syntaxTree
                    .GetRoot()
                    .DescendantNodes()
                    .OfType<ClassDeclarationSyntax>()
            )
            {
                if (semanticModel.GetDeclaredSymbol(declaration) is INamedTypeSymbol symbol)
                {
                    yield return new ClassEntry(symbol);
                }
            }
        }
    }

    private static bool HasTypedUseOutsideImplementation(
        AnalysisContext context,
        INamedTypeSymbol interfaceType,
        INamedTypeSymbol implementation
    )
    {
        foreach (var syntaxTree in context.Project.SyntaxTrees)
        {
            var semanticModel = context.Project.GetSemanticModel(syntaxTree);
            foreach (var name in syntaxTree.GetRoot().DescendantNodes().OfType<SimpleNameSyntax>())
            {
                if (
                    !SymbolEqualityComparer.Default.Equals(
                        semanticModel.GetSymbolInfo(name).Symbol,
                        interfaceType
                    )
                )
                {
                    continue;
                }

                if (name.Ancestors().OfType<BaseListSyntax>().Any())
                {
                    continue;
                }

                var containingType = semanticModel
                    .GetEnclosingSymbol(name.SpanStart)
                    ?.ContainingType;
                if (!SymbolEqualityComparer.Default.Equals(containingType, implementation))
                {
                    return true;
                }
            }
        }

        return false;
    }

    private sealed record InterfaceEntry(
        InterfaceDeclarationSyntax Declaration,
        INamedTypeSymbol Symbol
    );

    private sealed record ClassEntry(INamedTypeSymbol Symbol);
}
