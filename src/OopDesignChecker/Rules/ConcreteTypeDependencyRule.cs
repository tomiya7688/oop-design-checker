using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using OopDesignChecker.Analysis;
using OopDesignChecker.Core;

namespace OopDesignChecker.Rules;

internal sealed class ConcreteTypeDependencyRule : IAnalysisRule
{
    public RuleDescriptor Descriptor { get; } =
        new("OOP305", "Concrete dependency despite abstraction", DesignDiagnosticSeverity.Warning);

    public IEnumerable<DesignDiagnostic> Analyze(AnalysisContext context)
    {
        foreach (var syntaxTree in context.Project.SyntaxTrees)
        {
            var semanticModel = context.Project.GetSemanticModel(syntaxTree);
            var root = syntaxTree.GetRoot();

            foreach (
                var constructor in root.DescendantNodes().OfType<ConstructorDeclarationSyntax>()
            )
            {
                foreach (var parameter in constructor.ParameterList.Parameters)
                {
                    if (
                        semanticModel.GetDeclaredSymbol(parameter)
                            is not IParameterSymbol parameterSymbol
                        || parameterSymbol.Type
                            is not INamedTypeSymbol { TypeKind: TypeKind.Class } concreteType
                    )
                    {
                        continue;
                    }

                    var abstraction = ProjectAbstractionClassifier.FindMeaningfulAbstraction(
                        concreteType
                    );
                    if (abstraction is null)
                    {
                        continue;
                    }

                    yield return DiagnosticFactory.Create(
                        Descriptor,
                        parameter.Identifier.GetLocation(),
                        $"This constructor depends on concrete type {concreteType.Name} even though project abstraction {abstraction.Name} defines a behavioral contract. Depend on the abstraction when replacement is part of the design.",
                        parameterSymbol.ToDisplayString()
                    );
                }
            }
        }
    }
}
