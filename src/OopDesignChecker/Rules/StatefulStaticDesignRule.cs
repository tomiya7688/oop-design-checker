using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using OopDesignChecker.Analysis;
using OopDesignChecker.Core;

namespace OopDesignChecker.Rules;

internal sealed class StatefulStaticDesignRule : IAnalysisRule
{
    public RuleDescriptor Descriptor { get; } =
        new("OOP105", "Stateful static design", DesignDiagnosticSeverity.Warning);

    public IEnumerable<DesignDiagnostic> Analyze(AnalysisContext context)
    {
        foreach (var syntaxTree in context.Project.SyntaxTrees)
        {
            var semanticModel = context.Project.GetSemanticModel(syntaxTree);
            var root = syntaxTree.GetRoot();

            foreach (var declaration in root.DescendantNodes().OfType<ClassDeclarationSyntax>())
            {
                if (
                    semanticModel.GetDeclaredSymbol(declaration)
                        is not INamedTypeSymbol { IsStatic: true } symbol
                    || !SymbolUtilities.IsPrimaryDeclaration(symbol, declaration)
                )
                {
                    continue;
                }

                var mutableFields = symbol
                    .GetMembers()
                    .OfType<IFieldSymbol>()
                    .Where(field => field.IsStatic && !field.IsConst && !field.IsReadOnly)
                    .ToArray();

                if (mutableFields.Length == 0)
                {
                    continue;
                }

                yield return DiagnosticFactory.Create(
                    Descriptor,
                    declaration.Identifier.GetLocation(),
                    $"This static class owns {mutableFields.Length} mutable shared field(s). Shared mutable state behaves like global state; prefer an object with an explicit lifetime.",
                    symbol.ToDisplayString()
                );
            }
        }
    }
}
