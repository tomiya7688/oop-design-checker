using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using OopDesignChecker.Analysis;
using OopDesignChecker.Core;

namespace OopDesignChecker.Rules;

internal sealed class GetterSetterOnlyObjectRule : IAnalysisRule
{
    private const int MinimumStateProperties = 4;
    private const int MaximumBehaviorMethods = 1;

    public RuleDescriptor Descriptor { get; } =
        new("OOP405", "Getter/setter-only object candidate", DesignDiagnosticSeverity.Attention);

    public IEnumerable<DesignDiagnostic> Analyze(AnalysisContext context)
    {
        foreach (var syntaxTree in context.Project.SyntaxTrees)
        {
            var semanticModel = context.Project.GetSemanticModel(syntaxTree);
            var root = syntaxTree.GetRoot();

            foreach (var declaration in root.DescendantNodes().OfType<ClassDeclarationSyntax>())
            {
                if (
                    semanticModel.GetDeclaredSymbol(declaration) is not INamedTypeSymbol symbol
                    || DataCarrierClassifier.IsExplicitDataCarrier(symbol)
                )
                {
                    continue;
                }

                var stateProperties = declaration
                    .Members.OfType<PropertyDeclarationSyntax>()
                    .Where(IsTrivialPublicStateProperty)
                    .ToArray();
                if (stateProperties.Length < MinimumStateProperties)
                {
                    continue;
                }

                var behaviorCount = declaration
                    .Members.OfType<MethodDeclarationSyntax>()
                    .Count(method =>
                        semanticModel.GetDeclaredSymbol(method)
                            is IMethodSymbol
                            {
                                IsStatic: false,
                                DeclaredAccessibility: Accessibility.Public,
                                MethodKind: MethodKind.Ordinary,
                            }
                    );
                if (behaviorCount > MaximumBehaviorMethods)
                {
                    continue;
                }

                yield return DiagnosticFactory.Create(
                    Descriptor,
                    declaration.Identifier.GetLocation(),
                    $"This object exposes {stateProperties.Length} trivial public state properties but only {behaviorCount} public behavior method(s). If it is a domain object rather than an explicit data carrier, move behavior that owns this state into the object.",
                    symbol.ToDisplayString()
                );
            }
        }
    }

    private static bool IsTrivialPublicStateProperty(PropertyDeclarationSyntax property)
    {
        if (!property.Modifiers.Any(SyntaxKind.PublicKeyword) || property.AccessorList is null)
        {
            return false;
        }

        var accessors = property.AccessorList.Accessors;
        return accessors.Count >= 1
            && accessors.All(accessor => accessor.Body is null && accessor.ExpressionBody is null);
    }
}
