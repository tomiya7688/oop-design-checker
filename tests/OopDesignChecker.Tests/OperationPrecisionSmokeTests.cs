using OopDesignChecker.Core;
using OopDesignChecker.Rules;

namespace OopDesignChecker.Tests;

internal static class OperationPrecisionSmokeTests
{
    public static void Run()
    {
        LargeLinearPrimaryOperationIsWarning();
        ComplexNestedPrimaryOperationIsWarning();
        CommentHeavySimpleOperationIsAllowed();
        ExternalFrameworkTypeOperationIsAllowed();
        CalledPrivateHelperIsAllowed();
        NestedLocalFunctionDoesNotInflateOuterOperation();
    }

    private static void LargeLinearPrimaryOperationIsWarning()
    {
        var statements = CreateRepeatedStatements(130, "value += 1;");
        var source = $$"""
            internal sealed class Processor
            {
                public int Run()
                {
                    var value = 0;
            {{Indent(statements)}}
                    return value;
                }
            }
            """;

        AssertSingle(source);
    }

    private static void ComplexNestedPrimaryOperationIsWarning()
    {
        var branches = string.Join(
            Environment.NewLine,
            Enumerable
                .Range(0, 18)
                .Select(index =>
                    $"if (value > {index}){Environment.NewLine}{{{Environment.NewLine}    value--;{Environment.NewLine}}}"
                )
        );
        var source = $$"""
            internal sealed class Processor
            {
                public int Run(int value)
                {
                    if (value > 0)
                    {
                        if (value > 1)
                        {
                            if (value > 2)
                            {
                                if (value > 3)
                                {
            {{Indent(branches, 20)}}
                                }
                            }
                        }
                    }

                    return value;
                }
            }
            """;

        AssertSingle(source);
    }

    private static void CommentHeavySimpleOperationIsAllowed()
    {
        var comments = string.Join(
            Environment.NewLine,
            Enumerable.Range(0, 140).Select(index => $"// phase note {index}")
        );
        var source = $$"""
            internal sealed class Processor
            {
                public int Run()
                {
            {{Indent(comments)}}
                    return 1;
                }
            }
            """;

        AssertNone(source);
    }

    private static void ExternalFrameworkTypeOperationIsAllowed()
    {
        var statements = CreateRepeatedStatements(130, "value += 1;");
        var source = $$"""
            internal class ProcessorException : System.Exception
            {
                public int BuildCode()
                {
                    var value = 0;
            {{Indent(statements)}}
                    return value;
                }
            }
            """;

        AssertNone(source);
    }

    private static void CalledPrivateHelperIsAllowed()
    {
        var statements = CreateRepeatedStatements(130, "value += 1;");
        var source = $$"""
            internal sealed class Processor
            {
                public int Run() => BuildValue();

                private int BuildValue()
                {
                    var value = 0;
            {{Indent(statements)}}
                    return value;
                }
            }
            """;

        AssertNone(source);
    }

    private static void NestedLocalFunctionDoesNotInflateOuterOperation()
    {
        var statements = CreateRepeatedStatements(130, "value += 1;");
        var source = $$"""
            internal sealed class Processor
            {
                public int Run()
                {
                    int BuildValue()
                    {
                        var value = 0;
            {{Indent(statements, 24)}}
                        return value;
                    }

                    return BuildValue();
                }
            }
            """;

        AssertNone(source);
    }

    private static string CreateRepeatedStatements(int count, string statement) =>
        string.Join(Environment.NewLine, Enumerable.Repeat(statement, count));

    private static string Indent(string text, int spaces = 12)
    {
        var prefix = new string(' ', spaces);
        return string.Join(
            Environment.NewLine,
            text.Split(Environment.NewLine).Select(line => prefix + line)
        );
    }

    private static void AssertSingle(string source)
    {
        var diagnostics = new OversizedMainOperationRule()
            .Analyze(TestProjectFactory.Create(source))
            .ToArray();
        if (
            diagnostics.Length != 1
            || diagnostics[0].Rule.Id != "OOP201"
            || diagnostics[0].Severity != DesignDiagnosticSeverity.Warning
        )
        {
            var found = string.Join(
                ", ",
                diagnostics.Select(item => $"{item.Rule.Id}:{item.Severity}")
            );
            throw new InvalidOperationException(
                $"Expected one OOP201:Warning diagnostic, found [{found}]."
            );
        }
    }

    private static void AssertNone(string source)
    {
        var diagnostics = new OversizedMainOperationRule()
            .Analyze(TestProjectFactory.Create(source))
            .ToArray();
        if (diagnostics.Length == 0)
        {
            return;
        }

        var found = string.Join(", ", diagnostics.Select(item => item.Rule.Id));
        throw new InvalidOperationException($"Expected no OOP201 diagnostic, found [{found}].");
    }
}
