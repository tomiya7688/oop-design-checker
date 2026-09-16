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
                    if (
                        abstraction is null
                        || UsesConcreteOnlyContract(
                            context,
                            parameterSymbol,
                            concreteType,
                            abstraction
                        )
                    )
                    {
                        continue;
                    }

                    yield return DiagnosticFactory.Create(
                        Descriptor,
                        parameter.Identifier.GetLocation(),
                        $"This constructor depends on concrete type {concreteType.Name} even though project abstraction {abstraction.Name} covers the behavior this consumer uses. Depend on the abstraction when replacement is part of the design.",
                        parameterSymbol.ToDisplayString()
                    );
                }
            }
        }
    }

    private static bool UsesConcreteOnlyContract(
        AnalysisContext context,
        IParameterSymbol parameter,
        INamedTypeSymbol concreteType,
        INamedTypeSymbol abstraction
    )
    {
        var trackedSymbols = new HashSet<ISymbol>(SymbolEqualityComparer.Default) { parameter };
        TrackAssignedMembers(context, parameter, trackedSymbols);

        foreach (var syntaxTree in context.Project.SyntaxTrees)
        {
            var semanticModel = context.Project.GetSemanticModel(syntaxTree);
            var root = syntaxTree.GetRoot();

            foreach (var declaration in root.DescendantNodes().OfType<ClassDeclarationSyntax>())
            {
                if (
                    semanticModel.GetDeclaredSymbol(declaration) is not INamedTypeSymbol declaredType
                    || !SymbolEqualityComparer.Default.Equals(
                        declaredType,
                        parameter.ContainingSymbol.ContainingType
                    )
                )
                {
                    continue;
                }

                if (
                    UsesConcreteOnlyMember(
                        declaration,
                        semanticModel,
                        trackedSymbols,
                        concreteType,
                        abstraction
                    )
                    || RequiresConcreteArgumentType(
                        context,
                        declaration,
                        semanticModel,
                        trackedSymbols,
                        abstraction
                    )
                )
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static void TrackAssignedMembers(
        AnalysisContext context,
        IParameterSymbol parameter,
        HashSet<ISymbol> trackedSymbols
    )
    {
        if (parameter.ContainingSymbol is not IMethodSymbol constructor)
        {
            return;
        }

        foreach (var syntaxReference in constructor.DeclaringSyntaxReferences)
        {
            if (syntaxReference.GetSyntax() is not ConstructorDeclarationSyntax declaration)
            {
                continue;
            }

            var semanticModel = context.Project.GetSemanticModel(declaration.SyntaxTree);
            foreach (var assignment in declaration.DescendantNodes().OfType<AssignmentExpressionSyntax>())
            {
                var rightSymbol = semanticModel.GetSymbolInfo(assignment.Right).Symbol;
                if (!SymbolEqualityComparer.Default.Equals(rightSymbol, parameter))
                {
                    continue;
                }

                var leftSymbol = semanticModel.GetSymbolInfo(assignment.Left).Symbol;
                if (
                    leftSymbol is IFieldSymbol or IPropertySymbol
                    && SymbolEqualityComparer.Default.Equals(
                        leftSymbol.ContainingType,
                        constructor.ContainingType
                    )
                )
                {
                    trackedSymbols.Add(leftSymbol);
                }
            }
        }
    }

    private static bool UsesConcreteOnlyMember(
        ClassDeclarationSyntax declaration,
        SemanticModel semanticModel,
        HashSet<ISymbol> trackedSymbols,
        INamedTypeSymbol concreteType,
        INamedTypeSymbol abstraction
    )
    {
        foreach (var access in declaration.DescendantNodes().OfType<MemberAccessExpressionSyntax>())
        {
            var receiver = semanticModel.GetSymbolInfo(access.Expression).Symbol;
            if (!ContainsSymbol(trackedSymbols, receiver))
            {
                continue;
            }

            var accessedMember = semanticModel.GetSymbolInfo(access).Symbol;
            if (
                accessedMember is null
                || accessedMember.ContainingType?.SpecialType == SpecialType.System_Object
                || IsProvidedByAbstraction(concreteType, abstraction, accessedMember)
            )
            {
                continue;
            }

            return true;
        }

        return false;
    }

    private static bool RequiresConcreteArgumentType(
        AnalysisContext context,
        ClassDeclarationSyntax declaration,
        SemanticModel semanticModel,
        HashSet<ISymbol> trackedSymbols,
        INamedTypeSymbol abstraction
    )
    {
        foreach (var argument in declaration.DescendantNodes().OfType<ArgumentSyntax>())
        {
            var argumentSymbol = semanticModel.GetSymbolInfo(argument.Expression).Symbol;
            if (!ContainsSymbol(trackedSymbols, argumentSymbol))
            {
                continue;
            }

            var convertedType = semanticModel.GetTypeInfo(argument.Expression).ConvertedType;
            if (
                convertedType is not null
                && !context.Project.Compilation.ClassifyConversion(abstraction, convertedType).IsImplicit
            )
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsProvidedByAbstraction(
        INamedTypeSymbol concreteType,
        INamedTypeSymbol abstraction,
        ISymbol accessedMember
    )
    {
        foreach (var contractType in abstraction.AllInterfaces.Prepend(abstraction))
        {
            foreach (var contractMember in contractType.GetMembers())
            {
                var implementation = concreteType.FindImplementationForInterfaceMember(contractMember);
                if (
                    SymbolEqualityComparer.Default.Equals(implementation, accessedMember)
                    || SymbolEqualityComparer.Default.Equals(
                        implementation?.OriginalDefinition,
                        accessedMember.OriginalDefinition
                    )
                )
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static bool ContainsSymbol(HashSet<ISymbol> symbols, ISymbol? candidate) =>
        candidate is not null && symbols.Contains(candidate);
}
