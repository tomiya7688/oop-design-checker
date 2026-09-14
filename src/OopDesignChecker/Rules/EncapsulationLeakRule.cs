using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
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

            foreach (var diagnostic in AnalyzeFields(root, semanticModel))
            {
                yield return diagnostic;
            }

            foreach (var property in root.DescendantNodes().OfType<PropertyDeclarationSyntax>())
            {
                if (semanticModel.GetDeclaredSymbol(property) is not IPropertySymbol propertySymbol)
                {
                    continue;
                }

                if (ExposesMutableStoredValue(property, propertySymbol, semanticModel))
                {
                    yield return DiagnosticFactory.Create(
                        Descriptor,
                        property.Identifier.GetLocation(),
                        "This property exposes mutable internal storage directly. Prefer a read-only view, copy, or behavior-oriented API.",
                        propertySymbol.ToDisplayString(),
                        DesignDiagnosticSeverity.Danger);
                }

                if (HasUnnecessarilyPublicSetter(context, property, propertySymbol))
                {
                    yield return DiagnosticFactory.Create(
                        Descriptor,
                        property.Identifier.GetLocation(),
                        "This public setter is only used by the declaring object. Narrow the setter visibility.",
                        propertySymbol.ToDisplayString());
                }
            }
        }
    }

    private IEnumerable<DesignDiagnostic> AnalyzeFields(
        SyntaxNode root,
        SemanticModel semanticModel)
    {
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
                : DesignDiagnosticSeverity.Danger;
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

    private static bool ExposesMutableStoredValue(
        PropertyDeclarationSyntax declaration,
        IPropertySymbol property,
        SemanticModel semanticModel)
    {
        if (property.GetMethod?.DeclaredAccessibility != Accessibility.Public
            || !MutableCollectionInspector.IsMutableCollection(property.Type))
        {
            return false;
        }

        if (declaration.ExpressionBody is not null)
        {
            return ReferencesStoredValue(declaration.ExpressionBody.Expression, semanticModel);
        }

        var getter = declaration.AccessorList?.Accessors
            .FirstOrDefault(accessor => accessor.IsKind(SyntaxKind.GetAccessorDeclaration));
        if (getter is null)
        {
            return false;
        }

        if (getter.Body is null && getter.ExpressionBody is null)
        {
            return true;
        }

        if (getter.ExpressionBody is not null)
        {
            return ReferencesStoredValue(getter.ExpressionBody.Expression, semanticModel);
        }

        if (getter.Body?.Statements is [ReturnStatementSyntax { Expression: not null } returnStatement])
        {
            return ReferencesStoredValue(returnStatement.Expression, semanticModel);
        }

        return false;
    }

    private static bool ReferencesStoredValue(ExpressionSyntax expression, SemanticModel semanticModel) =>
        semanticModel.GetSymbolInfo(expression).Symbol is
            IFieldSymbol { IsStatic: false }
            or IPropertySymbol { IsStatic: false };

    private static bool HasUnnecessarilyPublicSetter(
        AnalysisContext context,
        PropertyDeclarationSyntax declaration,
        IPropertySymbol property)
    {
        if (!context.Project.IsApplication
            || property.SetMethod?.DeclaredAccessibility != Accessibility.Public
            || property.IsAbstract
            || property.IsVirtual
            || property.IsOverride
            || property.ContainingType.TypeKind == TypeKind.Interface
            || ImplementsInterfaceProperty(property)
            || declaration.AccessorList?.Accessors.Any(
                accessor => accessor.IsKind(SyntaxKind.InitAccessorDeclaration)) == true)
        {
            return false;
        }

        var writeScope = PropertyWriteInspector.GetWriteScope(context, property);
        return writeScope.HasInternalWrite && !writeScope.HasExternalWrite;
    }

    private static bool ImplementsInterfaceProperty(IPropertySymbol property)
    {
        foreach (var interfaceType in property.ContainingType.AllInterfaces)
        {
            foreach (var interfaceProperty in interfaceType.GetMembers().OfType<IPropertySymbol>())
            {
                var implementation = property.ContainingType.FindImplementationForInterfaceMember(interfaceProperty);
                if (SymbolEqualityComparer.Default.Equals(implementation, property))
                {
                    return true;
                }
            }
        }

        return false;
    }
}
