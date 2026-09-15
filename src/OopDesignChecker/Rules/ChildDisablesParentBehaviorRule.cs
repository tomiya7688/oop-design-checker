using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using OopDesignChecker.Analysis;
using OopDesignChecker.Core;

namespace OopDesignChecker.Rules;

internal sealed class ChildDisablesParentBehaviorRule : IAnalysisRule
{
    public RuleDescriptor Descriptor { get; } =
        new("OOP303", "Child disables parent behavior", DesignDiagnosticSeverity.Danger);

    public IEnumerable<DesignDiagnostic> Analyze(AnalysisContext context)
    {
        foreach (var syntaxTree in context.Project.SyntaxTrees)
        {
            var semanticModel = context.Project.GetSemanticModel(syntaxTree);
            var root = syntaxTree.GetRoot();

            foreach (var method in root.DescendantNodes().OfType<MethodDeclarationSyntax>())
            {
                if (
                    semanticModel.GetDeclaredSymbol(method)
                    is not IMethodSymbol { IsOverride: true } methodSymbol
                )
                {
                    continue;
                }

                var thrownType = InheritanceContractClassifier.GetSoleRejectionException(method);
                if (thrownType is null || !DisablesSupportedParentOperation(methodSymbol))
                {
                    continue;
                }

                yield return DiagnosticFactory.Create(
                    Descriptor,
                    method.Identifier.GetLocation(),
                    $"This override disables required or supported inherited behavior by always throwing {thrownType}.",
                    methodSymbol.ToDisplayString()
                );
            }
        }
    }

    private static bool DisablesSupportedParentOperation(IMethodSymbol method) =>
        InheritanceContractClassifier.ClassifyParentOperation(method)
            is ParentOperationContract.Required
                or ParentOperationContract.Supported
                or ParentOperationContract.OptionalNoOp;
}
