using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using OopDesignChecker.Analysis;

namespace OopDesignChecker.Rules;

internal static class ProjectAbstractionClassifier
{
    public static INamedTypeSymbol? FindMeaningfulAbstraction(
        INamedTypeSymbol concreteType,
        AnalysisContext context
    ) =>
        concreteType.AllInterfaces.FirstOrDefault(interfaceType =>
            IsMeaningfulAbstraction(interfaceType, context)
        );

    public static bool IsMeaningfulAbstraction(
        INamedTypeSymbol interfaceType,
        AnalysisContext context
    ) =>
        IsSourceDefinedInterface(interfaceType)
        && HasBehavioralContract(interfaceType)
        && (HasMultipleSourceImplementations(interfaceType, context) || HasDependencyUsage(interfaceType, context));

    private static bool IsSourceDefinedInterface(INamedTypeSymbol interfaceType) =>
        interfaceType.TypeKind == TypeKind.Interface
        && interfaceType.Locations.Any(location => location.IsInSource);

    private static bool HasBehavioralContract(INamedTypeSymbol interfaceType) =>
        interfaceType.GetMembers().Any(member =>
            member is IMethodSymbol { MethodKind: MethodKind.Ordinary }
                or IPropertySymbol
                or IEventSymbol
        )
        || interfaceType.AllInterfaces.Any(parent =>
            parent.GetMembers().Any(member =>
                member is IMethodSymbol { MethodKind: MethodKind.Ordinary }
                    or IPropertySymbol
                    or IEventSymbol
            )
        );

    private static bool HasMultipleSourceImplementations(
        INamedTypeSymbol interfaceType,
        AnalysisContext context
    )
    {
        var implementations = 0;

        foreach (var syntaxTree in context.Project.SyntaxTrees)
        {
            var semanticModel = context.Project.GetSemanticModel(syntaxTree);
            foreach (var declaration in syntaxTree.GetRoot().DescendantNodes().OfType<TypeDeclarationSyntax>())
            {
                if (
                    semanticModel.GetDeclaredSymbol(declaration) is not INamedTypeSymbol candidate
                    || candidate.TypeKind is not (TypeKind.Class or TypeKind.Struct)
                    || !candidate.AllInterfaces.Any(implemented =>
                        SymbolEqualityComparer.Default.Equals(implemented, interfaceType)
                    )
                )
                {
                    continue;
                }

                implementations++;
                if (implementations >= 2)
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static bool HasDependencyUsage(
        INamedTypeSymbol interfaceType,
        AnalysisContext context
    )
    {
        foreach (var syntaxTree in context.Project.SyntaxTrees)
        {
            var semanticModel = context.Project.GetSemanticModel(syntaxTree);
            var root = syntaxTree.GetRoot();

            foreach (var parameter in root.DescendantNodes().OfType<ParameterSyntax>())
            {
                if (
                    semanticModel.GetDeclaredSymbol(parameter) is IParameterSymbol parameterSymbol
                    && SymbolEqualityComparer.Default.Equals(parameterSymbol.Type, interfaceType)
                )
                {
                    return true;
                }
            }

            foreach (var field in root.DescendantNodes().OfType<FieldDeclarationSyntax>())
            {
                if (
                    semanticModel.GetTypeInfo(field.Declaration.Type).Type is ITypeSymbol fieldType
                    && SymbolEqualityComparer.Default.Equals(fieldType, interfaceType)
                )
                {
                    return true;
                }
            }

            foreach (var property in root.DescendantNodes().OfType<PropertyDeclarationSyntax>())
            {
                if (
                    semanticModel.GetTypeInfo(property.Type).Type is ITypeSymbol propertyType
                    && SymbolEqualityComparer.Default.Equals(propertyType, interfaceType)
                )
                {
                    return true;
                }
            }
        }

        return false;
    }
}
