using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using OopDesignChecker.Analysis;
using OopDesignChecker.Core;

namespace OopDesignChecker.Rules;

internal sealed class StaticMemberCandidateRule : IAnalysisRule
{
    public RuleDescriptor Descriptor { get; } =
        new("OOP103", "Static member candidate", DesignDiagnosticSeverity.Attention);

    public IEnumerable<DesignDiagnostic> Analyze(AnalysisContext context)
    {
        foreach (var syntaxTree in context.Project.SyntaxTrees)
        {
            var semanticModel = context.Project.GetSemanticModel(syntaxTree);
            var root = syntaxTree.GetRoot();

            foreach (var declaration in root.DescendantNodes().OfType<ClassDeclarationSyntax>())
            {
                if (
                    semanticModel.GetDeclaredSymbol(declaration) is not INamedTypeSymbol classSymbol
                    || HasUnresolvedBaseContract(declaration, semanticModel)
                    || FrameworkContractClassifier.HasExternalBaseTypeContract(classSymbol)
                    || FrameworkContractClassifier.HasExternalFrameworkAttribute(classSymbol)
                )
                {
                    continue;
                }

                if (
                    StaticEligibilityEvaluator.CanClassBeStatic(
                        declaration,
                        classSymbol,
                        semanticModel
                    )
                )
                {
                    continue;
                }

                foreach (var method in declaration.Members.OfType<MethodDeclarationSyntax>())
                {
                    if (
                        semanticModel.GetDeclaredSymbol(method) is not IMethodSymbol methodSymbol
                        || methodSymbol.IsStatic
                        || methodSymbol.IsAbstract
                        || methodSymbol.IsVirtual
                        || methodSymbol.IsOverride
                        || method.ExplicitInterfaceSpecifier is not null
                        || FrameworkContractClassifier.HasExternalFrameworkAttribute(methodSymbol)
                        || SymbolUtilities.ImplementsInterfaceMember(
                            methodSymbol,
                            declaration,
                            semanticModel
                        )
                    )
                    {
                        continue;
                    }

                    if (
                        InstanceUsageInspector.UsesInstanceState(
                            method,
                            methodSymbol,
                            semanticModel
                        )
                    )
                    {
                        continue;
                    }

                    yield return DiagnosticFactory.Create(
                        Descriptor,
                        method.Identifier.GetLocation(),
                        "This method does not use instance state and can be static.",
                        methodSymbol.ToDisplayString()
                    );
                }
            }
        }
    }

    private static bool HasUnresolvedBaseContract(
        ClassDeclarationSyntax declaration,
        SemanticModel semanticModel
    ) =>
        declaration.BaseList?.Types.Any(baseType =>
            semanticModel.GetTypeInfo(baseType.Type).Type is null or { TypeKind: TypeKind.Error }
        ) == true;
}
