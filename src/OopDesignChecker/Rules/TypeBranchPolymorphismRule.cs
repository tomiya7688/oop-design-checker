using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using OopDesignChecker.Analysis;
using OopDesignChecker.Core;

namespace OopDesignChecker.Rules;

internal sealed class TypeBranchPolymorphismRule : IAnalysisRule
{
    public RuleDescriptor Descriptor { get; } =
        new("OOP002", "Polymorphism bypassed by type branching", DesignDiagnosticSeverity.Warning);

    public IEnumerable<DesignDiagnostic> Analyze(AnalysisContext context)
    {
        foreach (var syntaxTree in context.Project.SyntaxTrees)
        {
            var semanticModel = context.Project.GetSemanticModel(syntaxTree);
            var root = syntaxTree.GetRoot();

            foreach (var ifStatement in root.DescendantNodes().OfType<IfStatementSyntax>())
            {
                if (IsElseIf(ifStatement))
                {
                    continue;
                }

                var tests = CollectTypeTests(ifStatement, semanticModel).ToArray();
                var suspiciousGroup = tests
                    .GroupBy(test => test.Subject, StringComparer.Ordinal)
                    .FirstOrDefault(group => HasCommonPolymorphicContract(group.ToArray()));

                if (suspiciousGroup is null)
                {
                    continue;
                }

                var typeNames = string.Join(
                    ", ",
                    suspiciousGroup.Select(test => test.Type.Name).Distinct(StringComparer.Ordinal)
                );
                yield return DiagnosticFactory.Create(
                    Descriptor,
                    ifStatement.IfKeyword.GetLocation(),
                    $"Repeated runtime type branching ({typeNames}) bypasses a shared source-defined type contract and may be replaceable with polymorphic behavior."
                );
            }
        }
    }

    private static bool IsElseIf(IfStatementSyntax statement) =>
        statement.Parent is ElseClauseSyntax { Statement: IfStatementSyntax };

    private static IEnumerable<TypeTest> CollectTypeTests(
        IfStatementSyntax root,
        SemanticModel semanticModel
    )
    {
        IfStatementSyntax? current = root;
        while (current is not null)
        {
            if (TryReadTypeTest(current.Condition, semanticModel, out var test))
            {
                yield return test;
            }

            current = current.Else?.Statement as IfStatementSyntax;
        }
    }

    private static bool TryReadTypeTest(
        ExpressionSyntax condition,
        SemanticModel semanticModel,
        out TypeTest test
    )
    {
        if (
            condition is BinaryExpressionSyntax binaryExpression
            && binaryExpression.RawKind == (int)SyntaxKind.IsExpression
            && semanticModel.GetTypeInfo(binaryExpression.Right).Type is INamedTypeSymbol binaryType
        )
        {
            test = new TypeTest(binaryExpression.Left.ToString(), binaryType);
            return true;
        }

        if (condition is IsPatternExpressionSyntax isPattern)
        {
            var typeSyntax = isPattern.Pattern switch
            {
                TypePatternSyntax typePattern => typePattern.Type,
                DeclarationPatternSyntax declarationPattern => declarationPattern.Type,
                _ => null,
            };

            if (
                typeSyntax is not null
                && semanticModel.GetTypeInfo(typeSyntax).Type is INamedTypeSymbol patternType
            )
            {
                test = new TypeTest(isPattern.Expression.ToString(), patternType);
                return true;
            }
        }

        test = default;
        return false;
    }

    private static bool HasCommonPolymorphicContract(IReadOnlyList<TypeTest> tests)
    {
        var types = new List<INamedTypeSymbol>();
        foreach (var test in tests)
        {
            if (
                test.Type.TypeKind != TypeKind.Class
                || !test.Type.Locations.Any(location => location.IsInSource)
                || types.Any(existing => SymbolEqualityComparer.Default.Equals(existing, test.Type))
            )
            {
                continue;
            }

            types.Add(test.Type);
        }

        if (types.Count < 2)
        {
            return false;
        }

        foreach (var candidate in EnumerateSourceClassContracts(types[0]))
        {
            if (types.Skip(1).All(type => SymbolUtilities.IsSameOrBaseType(candidate, type)))
            {
                return true;
            }
        }

        foreach (
            var interfaceType in types[0]
                .AllInterfaces.Where(ProjectAbstractionClassifier.IsMeaningfulAbstraction)
        )
        {
            if (
                types
                    .Skip(1)
                    .All(type =>
                        type.AllInterfaces.Any(implemented =>
                            SymbolEqualityComparer.Default.Equals(implemented, interfaceType)
                        )
                    )
            )
            {
                return true;
            }
        }

        return false;
    }

    private static IEnumerable<INamedTypeSymbol> EnumerateSourceClassContracts(
        INamedTypeSymbol type
    )
    {
        INamedTypeSymbol? current = type;
        while (current is not null && current.SpecialType != SpecialType.System_Object)
        {
            if (current.Locations.Any(location => location.IsInSource))
            {
                yield return current;
            }

            current = current.BaseType;
        }
    }

    private readonly record struct TypeTest(string Subject, INamedTypeSymbol Type);
}
