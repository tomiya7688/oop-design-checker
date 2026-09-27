using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using OopDesignChecker.Analysis;
using OopDesignChecker.Core;

namespace OopDesignChecker.Rules;

internal sealed class AvoidableConcreteConstructionRule : IAnalysisRule
{
    private static readonly string[] CompositionTypeSuffixes =
    [
        "CompositionRoot",
        "Bootstrap",
        "Bootstrapper",
        "Startup",
        "HostBuilder",
        "AppHost",
    ];

    private static readonly string[] CompositionMethodPrefixes =
    [
        "Build",
        "Compose",
        "Configure",
        "Create",
        "Initialize",
        "Register",
        "Setup",
        "Wire",
    ];

    public RuleDescriptor Descriptor { get; } =
        new(
            "OOP306",
            "Avoidable concrete construction dependency",
            DesignDiagnosticSeverity.Warning
        );

    public IEnumerable<DesignDiagnostic> Analyze(AnalysisContext context)
    {
        foreach (var syntaxTree in context.Project.SyntaxTrees)
        {
            var semanticModel = context.Project.GetSemanticModel(syntaxTree);
            var root = syntaxTree.GetRoot();

            foreach (
                var creation in root.DescendantNodes().OfType<ObjectCreationExpressionSyntax>()
            )
            {
                if (
                    semanticModel.GetTypeInfo(creation).Type is not INamedTypeSymbol concreteType
                    || concreteType.TypeKind != TypeKind.Class
                    || IsOwnedConstruction(creation, semanticModel)
                )
                {
                    continue;
                }

                var abstraction = ProjectAbstractionClassifier.FindMeaningfulAbstraction(
                    concreteType
                );
                if (abstraction is null || !HasBehavior(concreteType))
                {
                    continue;
                }

                var containingType = semanticModel
                    .GetEnclosingSymbol(creation.SpanStart)
                    ?.ContainingType;
                if (
                    containingType is null
                    || SymbolEqualityComparer.Default.Equals(containingType, concreteType)
                )
                {
                    continue;
                }

                yield return DiagnosticFactory.Create(
                    Descriptor,
                    creation.NewKeyword.GetLocation(),
                    $"{containingType.Name} directly constructs replaceable collaborator {concreteType.Name}, while project abstraction {abstraction.Name} defines a behavioral contract. Prefer receiving the abstraction when the collaborator lifecycle is not owned here.",
                    containingType.ToDisplayString()
                );
            }
        }
    }

    private static bool HasBehavior(INamedTypeSymbol type) =>
        type.GetMembers()
            .OfType<IMethodSymbol>()
            .Any(method => method.MethodKind == MethodKind.Ordinary && !method.IsStatic);

    private static bool IsOwnedConstruction(
        ObjectCreationExpressionSyntax creation,
        SemanticModel semanticModel
    ) =>
        IsDirectReturn(creation)
        || IsNestedObjectGraphConstruction(creation)
        || creation.Ancestors().OfType<CollectionExpressionSyntax>().Any()
        || IsCompositionRootLocalWiring(creation, semanticModel);

    private static bool IsDirectReturn(ObjectCreationExpressionSyntax creation)
    {
        SyntaxNode expression = creation;

        while (true)
        {
            if (expression.Parent is ParenthesizedExpressionSyntax or CastExpressionSyntax)
            {
                expression = expression.Parent;
                continue;
            }

            if (expression.Parent is SwitchExpressionArmSyntax arm)
            {
                expression = arm;
                continue;
            }

            if (
                expression is SwitchExpressionArmSyntax
                && expression.Parent is SwitchExpressionSyntax switchExpression
            )
            {
                expression = switchExpression;
                continue;
            }

            if (
                expression.Parent is ConditionalExpressionSyntax conditional
                && (conditional.WhenTrue == expression || conditional.WhenFalse == expression)
            )
            {
                expression = conditional;
                continue;
            }

            return expression.Parent is ReturnStatementSyntax or ArrowExpressionClauseSyntax;
        }
    }

    private static bool IsNestedObjectGraphConstruction(ObjectCreationExpressionSyntax creation)
    {
        var argument = creation.Ancestors().OfType<ArgumentSyntax>().FirstOrDefault();
        return argument?.Ancestors().OfType<ObjectCreationExpressionSyntax>().Any() == true;
    }

    private static bool IsCompositionRootLocalWiring(
        ObjectCreationExpressionSyntax creation,
        SemanticModel semanticModel
    )
    {
        SyntaxNode expression = creation;
        while (expression.Parent is ParenthesizedExpressionSyntax or CastExpressionSyntax)
        {
            expression = expression.Parent;
        }

        if (
            expression.Parent
                is not EqualsValueClauseSyntax { Parent: VariableDeclaratorSyntax variable }
            || semanticModel.GetDeclaredSymbol(variable) is not ILocalSymbol local
            || semanticModel.GetEnclosingSymbol(creation.SpanStart) is not IMethodSymbol method
            || !IsCompositionContext(method)
        )
        {
            return false;
        }

        var scope = variable.Ancestors().OfType<BaseMethodDeclarationSyntax>().FirstOrDefault();
        if (scope is null)
        {
            return false;
        }

        var references = scope
            .DescendantNodes()
            .OfType<IdentifierNameSyntax>()
            .Where(identifier =>
                SymbolEqualityComparer.Default.Equals(
                    semanticModel.GetSymbolInfo(identifier).Symbol,
                    local
                )
            )
            .ToArray();

        return references.Length > 0 && references.All(IsDirectConstructorArgument);
    }

    private static bool IsDirectConstructorArgument(IdentifierNameSyntax reference)
    {
        SyntaxNode expression = reference;
        while (expression.Parent is ParenthesizedExpressionSyntax or CastExpressionSyntax)
        {
            expression = expression.Parent;
        }

        return expression.Parent is ArgumentSyntax argument
            && argument.Ancestors().OfType<ObjectCreationExpressionSyntax>().Any();
    }

    private static bool IsCompositionContext(IMethodSymbol method)
    {
        if (method.IsStatic && method.Name == "Main")
        {
            return true;
        }

        var containingTypeName = method.ContainingType?.Name;
        if (containingTypeName is null || !IsCompositionTypeName(containingTypeName))
        {
            return false;
        }

        if (method.MethodKind == MethodKind.Constructor)
        {
            return true;
        }

        return CompositionMethodPrefixes.Any(prefix =>
            method.Name.StartsWith(prefix, StringComparison.Ordinal)
        );
    }

    private static bool IsCompositionTypeName(string typeName) =>
        typeName == "Program"
        || CompositionTypeSuffixes.Any(suffix =>
            typeName.EndsWith(suffix, StringComparison.Ordinal)
        );
}
