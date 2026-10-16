using OopDesignChecker.Core;
using OopDesignChecker.Rules;

namespace OopDesignChecker.Tests;

internal static class OperationPrecisionSmokeTests
{
    public static void Run()
    {
        大きな直線処理は警告になる();
        複雑で深く入れ子の主処理は警告になる();
        コメントが多くても単純な処理は許可する();
        外部フレームワーク型の処理は許可する();
        通常の外部属性は大きな処理の診断を抑制しない();
        シリアライズコールバックを過大な主処理としない();
        モジュール初期化子を過大な主処理としない();
        呼び出されるprivate補助処理は許可する();
        入れ子のローカル関数で外側処理を過大評価しない();
    }

    private static void 大きな直線処理は警告になる()
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

    private static void 複雑で深く入れ子の主処理は警告になる()
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

    private static void コメントが多くても単純な処理は許可する()
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

    private static void 外部フレームワーク型の処理は許可する()
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

    private static void 通常の外部属性は大きな処理の診断を抑制しない()
    {
        var statements = CreateRepeatedStatements(130, "value += 1;");
        var source = $$"""
            internal sealed class Processor
            {
                [System.Obsolete]
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

    private static void シリアライズコールバックを過大な主処理としない()
    {
        var statements = CreateRepeatedStatements(130, "value += 1;");
        var source = $$"""
            using System.Runtime.Serialization;

            internal sealed class Processor
            {
                [OnDeserialized]
                public void Restore(StreamingContext context)
                {
                    var value = 0;
            {{Indent(statements)}}
                    _ = value;
                }
            }
            """;

        AssertNone(source);
    }

    private static void モジュール初期化子を過大な主処理としない()
    {
        var statements = CreateRepeatedStatements(130, "value += 1;");
        var source = $$"""
            using System.Runtime.CompilerServices;

            internal static class Processor
            {
                [ModuleInitializer]
                internal static void Initialize()
                {
                    var value = 0;
            {{Indent(statements)}}
                    _ = value;
                }
            }
            """;

        AssertNone(source);
    }

    private static void 呼び出されるprivate補助処理は許可する()
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

    private static void 入れ子のローカル関数で外側処理を過大評価しない()
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
        var diagnostics = new 過大主処理規則()
            .解析する(TestProjectFactory.解析文脈を作る(source))
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
        var diagnostics = new 過大主処理規則()
            .解析する(TestProjectFactory.解析文脈を作る(source))
            .ToArray();
        if (diagnostics.Length == 0)
        {
            return;
        }

        var found = string.Join(", ", diagnostics.Select(item => item.Rule.Id));
        throw new InvalidOperationException($"Expected no OOP201 diagnostic, found [{found}].");
    }
}
