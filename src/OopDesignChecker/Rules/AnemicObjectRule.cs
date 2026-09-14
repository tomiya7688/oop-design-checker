using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using OopDesignChecker.Analysis;
using OopDesignChecker.Core;

namespace OopDesignChecker.Rules;

internal sealed class AnemicObjectRule : IAnalysisRule
{
    private const int MinimumStateProperties = 3;
    private const int MinimumExternallyUsedMembers = 3;

    public RuleDescriptor Descriptor { get; } =
        new("OOP402", "Anemic object candidate", DesignDiagnosticSeverity.Warning);

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
                    || IsExplicitDataCarrier(symbol)
                    || CountPublicStateProperties(declaration) < MinimumStateProperties
                    || CountPublicBehaviorMethods(declaration, semanticModel) > 1
                )
                {
                    continue;
                }

                var externalBehavior = FindExternalBehavior(context, symbol);
                if (externalBehavior is null)
                {
                    continue;
                }

                yield return DiagnosticFactory.Create(
                    Descriptor,
                    declaration.Identifier.GetLocation(),
                    $"{symbol.Name} mostly exposes state while {externalBehavior.Name} performs behavior by directly using several of its members. Consider moving state-owning behavior into the object.",
                    symbol.ToDisplayString()
                );
            }
        }
    }

    private static int CountPublicStateProperties(ClassDeclarationSyntax declaration) =>
        declaration
            .Members.OfType<PropertyDeclarationSyntax>()
            .Count(property =>
                property.Modifiers.Any(SyntaxKind.PublicKeyword)
                && property.AccessorList is not null
                && property.AccessorList.Accessors.All(accessor =>
                    accessor.Body is null && accessor.ExpressionBody is null
                )
            );

    private static int CountPublicBehaviorMethods(
        ClassDeclarationSyntax declaration,
        SemanticModel semanticModel
    ) =>
        declaration
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

    private static IMethodSymbol? FindExternalBehavior(
        AnalysisContext context,
        INamedTypeSymbol candidate
    )
    {
        foreach (var syntaxTree in context.Project.SyntaxTrees)
        {
            var semanticModel = context.Project.GetSemanticModel(syntaxTree);
            foreach (
                var method in syntaxTree
                    .GetRoot()
                    .DescendantNodes()
                    .OfType<MethodDeclarationSyntax>()
            )
            {
                if (
                    semanticModel.GetDeclaredSymbol(method) is not IMethodSymbol methodSymbol
                    || SymbolEqualityComparer.Default.Equals(methodSymbol.ContainingType, candidate)
                    || IsProjectionInfrastructure(methodSymbol.ContainingType)
                )
                {
                    continue;
                }

                foreach (var parameter in method.ParameterList.Parameters)
                {
                    if (
                        semanticModel.GetDeclaredSymbol(parameter)
                            is not IParameterSymbol parameterSymbol
                        || !SymbolEqualityComparer.Default.Equals(parameterSymbol.Type, candidate)
                    )
                    {
                        continue;
                    }

                    var members = method
                        .DescendantNodes()
                        .OfType<MemberAccessExpressionSyntax>()
                        .Where(access =>
                            ReceiverIsParameter(access, parameterSymbol, semanticModel)
                        )
                        .Select(access => semanticModel.GetSymbolInfo(access).Symbol)
                        .Where(member =>
                            SymbolEqualityComparer.Default.Equals(member?.ContainingType, candidate)
                        )
                        .Select(member => member!.Name)
                        .Distinct(StringComparer.Ordinal)
                        .ToArray();
                    if (members.Length >= MinimumExternallyUsedMembers)
                    {
                        return methodSymbol;
                    }
                }
            }
        }

        return null;
    }

    private static bool ReceiverIsParameter(
        MemberAccessExpressionSyntax access,
        IParameterSymbol parameter,
        SemanticModel semanticModel
    ) =>
        SymbolEqualityComparer.Default.Equals(
            semanticModel.GetSymbolInfo(access.Expression).Symbol,
            parameter
        );

    private static bool IsExplicitDataCarrier(INamedTypeSymbol type)
    {
        var name = type.Name;
        return HasDataCarrierMarker(type)
            || name.EndsWith("Dto", StringComparison.OrdinalIgnoreCase)
            || name.EndsWith("Model", StringComparison.OrdinalIgnoreCase)
            || name.EndsWith("Request", StringComparison.OrdinalIgnoreCase)
            || name.EndsWith("Response", StringComparison.OrdinalIgnoreCase)
            || name.EndsWith("Options", StringComparison.OrdinalIgnoreCase)
            || name.EndsWith("Configuration", StringComparison.OrdinalIgnoreCase)
            || name.EndsWith("Config", StringComparison.OrdinalIgnoreCase)
            || name.EndsWith("Message", StringComparison.OrdinalIgnoreCase)
            || name.EndsWith("Event", StringComparison.OrdinalIgnoreCase)
            || name.EndsWith("Command", StringComparison.OrdinalIgnoreCase)
            || name.EndsWith("Payload", StringComparison.OrdinalIgnoreCase)
            || name.EndsWith("Record", StringComparison.OrdinalIgnoreCase)
            || name.EndsWith("Row", StringComparison.OrdinalIgnoreCase)
            || name.EndsWith("Document", StringComparison.OrdinalIgnoreCase)
            || name.EndsWith("ViewModel", StringComparison.OrdinalIgnoreCase);
    }

    private static bool HasDataCarrierMarker(INamedTypeSymbol type) =>
        type.GetAttributes().Any(attribute =>
            attribute.AttributeClass?.Name
                is "SerializableAttribute"
                    or "DataContractAttribute"
                    or "JsonObjectAttribute"
                    or "MessagePackObjectAttribute"
                    or "ProtoContractAttribute"
        );

    private static bool IsProjectionInfrastructure(INamedTypeSymbol type)
    {
        var name = type.Name;
        return name.EndsWith("Mapper", StringComparison.OrdinalIgnoreCase)
            || name.EndsWith("Formatter", StringComparison.OrdinalIgnoreCase)
            || name.EndsWith("Serializer", StringComparison.OrdinalIgnoreCase)
            || name.EndsWith("Converter", StringComparison.OrdinalIgnoreCase)
            || name.EndsWith("Projection", StringComparison.OrdinalIgnoreCase)
            || name.EndsWith("Presenter", StringComparison.OrdinalIgnoreCase)
            || name.EndsWith("Factory", StringComparison.OrdinalIgnoreCase);
    }
}
