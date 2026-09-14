using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using OopDesignChecker.Analysis;
using OopDesignChecker.Core;

namespace OopDesignChecker.Rules;

internal sealed class EncapsulationLeakRule : IAnalysisRule
{
    public RuleDescriptor Descriptor { get; } = new(
        "OOP106",
        "Encapsulation leak",
        DesignDiagnosticSeverity.Warning);

    public IEnumerable<DesignDiagnostic> Analyze(AnalysisContext context)
    {
        foreach (var syntaxTree in context.Project.SyntaxTrees)
        {
            var semanticModel = context.Project.GetSemanticModel(syntaxTree);
            var root = syntaxTree.GetRoot();

            foreach (var variable in root.DescendantNodes().OfType<VariableDeclaratorSyntax>())
            {
                if (variable.Parent?.Parent is not FieldDeclarationSyntax)
                {
                    continue;
                }

                if (semanticModel.GetDeclaredSymbol(variable) is not IFieldSymbol field
                    || field.DeclaredAccessibility != Accessibility.Public
                    || field.IsConst)
                {
                    continue;
                }

                var severity = field.IsReadOnly
                    ? DesignDiagnosticSeverity.Warning
                    : DesignDiagnosticSeverity.Error;
                var message = field.IsReadOnly
                    ? "A public field exposes the object's representation. Prefer a property or behavior-oriented API."
                    : "A public mutable field allows callers to bypass encapsulation.";

                yield return DiagnosticFactory.Create(
                    Descriptor,
                    variable.GetLocation(),
                    message,
                    field.ToDisplayString(),
                    severity);
            }
        }
    }
}
