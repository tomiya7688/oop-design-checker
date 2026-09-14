using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using OopDesignChecker.Analysis;

namespace OopDesignChecker.Rules;

internal static class StaticEligibilityEvaluator
{
    public static bool CanClassBeStatic(
        ClassDeclarationSyntax declaration,
        INamedTypeSymbol symbol,
        SemanticModel semanticModel)
    {
        if (symbol.IsStatic || symbol.IsAbstract || symbol.Interfaces.Length > 0)
        {
            return false;
        }

        if (symbol.BaseType is { SpecialType: not SpecialType.System_Object })
        {
            return false;
        }

        if (symbol.InstanceConstructors.Any(constructor => !constructor.IsImplicitlyDeclared && constructor.Parameters.Length > 0))
        {
            return false;
        }

        if (symbol.GetMembers().Any(member => member switch
            {
                IFieldSymbol { IsStatic: false } => true,
                IPropertySymbol { IsStatic: false } => true,
                IEventSymbol { IsStatic: false } => true,
                _ => false
            }))
        {
            return false;
        }

        var instanceMethods = declaration.Members
            .OfType<MethodDeclarationSyntax>()
            .Select(method => (Syntax: method, Symbol: semanticModel.GetDeclaredSymbol(method) as IMethodSymbol))
            .Where(pair => pair.Symbol is { IsStatic: false })
            .ToArray();

        if (instanceMethods.Length == 0)
        {
            return symbol.GetMembers().Any(member => member.IsStatic);
        }

        foreach (var pair in instanceMethods)
        {
            var methodSymbol = pair.Symbol!;
            if (methodSymbol.IsAbstract || methodSymbol.IsVirtual || methodSymbol.IsOverride)
            {
                return false;
            }

            if (pair.Syntax.ExplicitInterfaceSpecifier is not null
                || InstanceUsageInspector.UsesInstanceState(pair.Syntax, methodSymbol, semanticModel))
            {
                return false;
            }
        }

        return true;
    }
}
