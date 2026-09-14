using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using OopDesignChecker.Analysis;
using OopDesignChecker.Core;

namespace OopDesignChecker.Rules;

internal sealed class ObjectInvariantBypassRule : IAnalysisRule
{
    public RuleDescriptor Descriptor { get; } =
        new("OOP107", "Object invariant can be bypassed", DesignDiagnosticSeverity.Danger);

    public IEnumerable<DesignDiagnostic> Analyze(AnalysisContext context)
    {
        foreach (var syntaxTree in context.Project.SyntaxTrees)
        {
            var semanticModel = context.Project.GetSemanticModel(syntaxTree);
            var root = syntaxTree.GetRoot();

            foreach (
                var classDeclaration in root.DescendantNodes().OfType<ClassDeclarationSyntax>()
            )
            {
                foreach (
                    var propertyDeclaration in classDeclaration.Members.OfType<PropertyDeclarationSyntax>()
                )
                {
                    if (
                        semanticModel.GetDeclaredSymbol(propertyDeclaration)
                            is not IPropertySymbol property
                        || property.SetMethod?.DeclaredAccessibility != Accessibility.Public
                        || property.IsStatic
                    )
                    {
                        continue;
                    }

                    var guardedMutation = classDeclaration
                        .Members.OfType<MethodDeclarationSyntax>()
                        .FirstOrDefault(method =>
                            HasGuardedAssignment(method, property, semanticModel)
                        );
                    if (guardedMutation is null)
                    {
                        continue;
                    }

                    yield return DiagnosticFactory.Create(
                        Descriptor,
                        propertyDeclaration.Identifier.GetLocation(),
                        $"{property.Name} has a public setter even though {guardedMutation.Identifier.ValueText} validates values before assigning it. Callers can bypass the object's invariant through the setter.",
                        property.ToDisplayString()
                    );
                }
            }
        }
    }

    private static bool HasGuardedAssignment(
        MethodDeclarationSyntax method,
        IPropertySymbol property,
        SemanticModel semanticModel
    )
    {
        if (
            semanticModel.GetDeclaredSymbol(method) is not IMethodSymbol methodSymbol
            || methodSymbol.IsStatic
        )
        {
            return false;
        }

        foreach (var assignment in method.DescendantNodes().OfType<AssignmentExpressionSyntax>())
        {
            if (
                !SymbolEqualityComparer.Default.Equals(
                    semanticModel.GetSymbolInfo(assignment.Left).Symbol,
                    property
                )
                || semanticModel.GetSymbolInfo(assignment.Right).Symbol
                    is not IParameterSymbol parameter
            )
            {
                continue;
            }

            if (
                method
                    .DescendantNodes()
                    .OfType<IfStatementSyntax>()
                    .Any(ifStatement =>
                        ReferencesParameter(ifStatement.Condition, parameter, semanticModel)
                        && ifStatement
                            .Statement.DescendantNodesAndSelf()
                            .OfType<ThrowStatementSyntax>()
                            .Any()
                    )
            )
            {
                return true;
            }
        }

        return false;
    }

    private static bool ReferencesParameter(
        ExpressionSyntax expression,
        IParameterSymbol parameter,
        SemanticModel semanticModel
    ) =>
        expression
            .DescendantNodesAndSelf()
            .OfType<IdentifierNameSyntax>()
            .Any(identifier =>
                SymbolEqualityComparer.Default.Equals(
                    semanticModel.GetSymbolInfo(identifier).Symbol,
                    parameter
                )
            );
}
