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
            var root = syntaxTree.GetRoot();

            foreach (var ifStatement in root.DescendantNodes().OfType<IfStatementSyntax>())
            {
                if (IsElseIf(ifStatement))
                {
                    continue;
                }

                var tests = CollectTypeTests(ifStatement).ToArray();
                var suspiciousGroup = tests
                    .GroupBy(test => test.Subject, StringComparer.Ordinal)
                    .FirstOrDefault(group =>
                        group.Select(test => test.TypeName).Distinct(StringComparer.Ordinal).Count()
                        >= 2
                    );

                if (suspiciousGroup is null)
                {
                    continue;
                }

                var typeNames = string.Join(
                    ", ",
                    suspiciousGroup.Select(test => test.TypeName).Distinct(StringComparer.Ordinal)
                );
                yield return DiagnosticFactory.Create(
                    Descriptor,
                    ifStatement.IfKeyword.GetLocation(),
                    $"Repeated runtime type branching ({typeNames}) may be replaceable with polymorphic behavior."
                );
            }
        }
    }

    private static bool IsElseIf(IfStatementSyntax statement) =>
        statement.Parent is ElseClauseSyntax { Statement: IfStatementSyntax };

    private static IEnumerable<TypeTest> CollectTypeTests(IfStatementSyntax root)
    {
        IfStatementSyntax? current = root;
        while (current is not null)
        {
            if (TryReadTypeTest(current.Condition, out var test))
            {
                yield return test;
            }

            current = current.Else?.Statement as IfStatementSyntax;
        }
    }

    private static bool TryReadTypeTest(ExpressionSyntax condition, out TypeTest test)
    {
        if (
            condition is BinaryExpressionSyntax binaryExpression
            && binaryExpression.RawKind == (int)SyntaxKind.IsExpression
        )
        {
            test = new TypeTest(
                binaryExpression.Left.ToString(),
                binaryExpression.Right.ToString()
            );
            return true;
        }

        if (condition is IsPatternExpressionSyntax isPattern)
        {
            var typeName = isPattern.Pattern switch
            {
                TypePatternSyntax typePattern => typePattern.Type.ToString(),
                DeclarationPatternSyntax declarationPattern => declarationPattern.Type.ToString(),
                _ => null,
            };

            if (typeName is not null)
            {
                test = new TypeTest(isPattern.Expression.ToString(), typeName);
                return true;
            }
        }

        test = default;
        return false;
    }

    private readonly record struct TypeTest(string Subject, string TypeName);
}
