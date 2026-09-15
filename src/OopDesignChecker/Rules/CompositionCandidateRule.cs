using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using OopDesignChecker.Analysis;
using OopDesignChecker.Core;

namespace OopDesignChecker.Rules;

internal sealed class CompositionCandidateRule : IAnalysisRule
{
    private const int MinimumProtectedMembersUsed = 2;

    public RuleDescriptor Descriptor { get; } =
        new(
            "OOP307",
            "Composition may be more appropriate than inheritance",
            DesignDiagnosticSeverity.Attention
        );

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
                    || !SymbolUtilities.IsPrimaryDeclaration(symbol, declaration)
                    || symbol.BaseType is not { } baseType
                    || baseType.SpecialType == SpecialType.System_Object
                    || baseType.IsAbstract
                    || !baseType.Locations.Any(location => location.IsInSource)
                    || HasBehaviorSpecialization(declaration, semanticModel)
                )
                {
                    continue;
                }

                var reuseOperation = declaration
                    .Members.OfType<MethodDeclarationSyntax>()
                    .Select(method => ReadProtectedReuse(method, semanticModel, baseType))
                    .Where(reuse => reuse is not null)
                    .OrderByDescending(reuse => reuse!.ProtectedMembersUsed)
                    .FirstOrDefault();
                if (reuseOperation is null)
                {
                    continue;
                }

                yield return DiagnosticFactory.Create(
                    Descriptor,
                    declaration.Identifier.GetLocation(),
                    $"Child operation {reuseOperation.OperationName} builds new behavior from {reuseOperation.ProtectedMembersUsed} protected members of concrete base {baseType.Name} without specializing parent behavior. Composition may express the relationship more accurately.",
                    symbol.ToDisplayString()
                );
            }
        }
    }

    private static bool HasBehaviorSpecialization(
        ClassDeclarationSyntax declaration,
        SemanticModel semanticModel
    ) =>
        declaration
            .Members.OfType<MethodDeclarationSyntax>()
            .Any(method =>
                semanticModel.GetDeclaredSymbol(method) is IMethodSymbol { IsOverride: true }
            );

    private static ProtectedReuse? ReadProtectedReuse(
        MethodDeclarationSyntax declaration,
        SemanticModel semanticModel,
        INamedTypeSymbol baseType
    )
    {
        if (
            semanticModel.GetDeclaredSymbol(declaration)
            is not IMethodSymbol
            {
                IsStatic: false,
                MethodKind: MethodKind.Ordinary,
                DeclaredAccessibility: not Accessibility.Private,
            } method
        )
        {
            return null;
        }

        var usedProtectedMembers = declaration
            .DescendantNodes()
            .OfType<SimpleNameSyntax>()
            .Select(name => semanticModel.GetSymbolInfo(name).Symbol)
            .Where(member => IsProtectedBaseMember(member, baseType))
            .Select(member => member!)
            .Distinct(SymbolEqualityComparer.Default)
            .Count();
        if (usedProtectedMembers < MinimumProtectedMembersUsed)
        {
            return null;
        }

        return new ProtectedReuse(method.Name, usedProtectedMembers);
    }

    private static bool IsProtectedBaseMember(ISymbol? member, INamedTypeSymbol baseType) =>
        member is not null
        && IsInstanceMember(member)
        && member.DeclaredAccessibility
            is Accessibility.Protected
                or Accessibility.ProtectedOrInternal
                or Accessibility.ProtectedAndInternal
        && member.ContainingType is not null
        && SymbolUtilities.IsSameOrBaseType(member.ContainingType, baseType);

    private static bool IsInstanceMember(ISymbol member) =>
        member switch
        {
            IMethodSymbol method => !method.IsStatic,
            IPropertySymbol property => !property.IsStatic,
            IFieldSymbol field => !field.IsStatic,
            IEventSymbol @event => !@event.IsStatic,
            _ => false,
        };

    private sealed record ProtectedReuse(string OperationName, int ProtectedMembersUsed);
}
