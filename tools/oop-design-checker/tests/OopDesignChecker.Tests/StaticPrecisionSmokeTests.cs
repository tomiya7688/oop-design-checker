using OopDesignChecker.Core;
using OopDesignChecker.Rules;

namespace OopDesignChecker.Tests;

internal static class 静的設計精度スモークテスト
{
    public static void Run()
    {
        OrdinaryExternalTypeAttributeDoesNotSuppressVisibilityDiagnostic();
        ExternalContractIsNotSealingCandidate();
        外部基底型の契約メソッドを静的化候補にしない();
        通常の外部型属性は静的クラス候補を抑制しない();
        通常の外部メソッド属性は静的メンバー候補を抑制しない();
        フレームワークのコールバックを静的化候補にしない();
        属性付き型を静的クラス候補にしない();
        SerializableTypeRemainsSealingCandidate();
        変更可能な静的プロパティは警告になる();
        読み取り専用宣言の可変コレクションは警告になる();
        スレッドローカルフィールドは許可する();
    }

    private static void OrdinaryExternalTypeAttributeDoesNotSuppressVisibilityDiagnostic()
    {
        const string source = """
            [System.Obsolete]
            public class Base
            {
            }

            internal class Child : Base
            {
            }
            """;

        AssertSingleRule(new 過剰可視性規則(), source, "OOP101");
    }

    private static void ExternalContractIsNotSealingCandidate()
    {
        const string source = """
            internal class SampleException : System.Exception
            {
            }
            """;

        AssertNone(new 封止候補規則(), source, "OOP102");
    }

    private static void 外部基底型の契約メソッドを静的化候補にしない()
    {
        const string source = """
            internal class SampleException : System.Exception
            {
                private int _count;

                public void Touch() => _count++;
                public string Describe() => "sample";
            }
            """;

        AssertNone(new 静的メンバー候補規則(), source, "OOP103");
    }

    private static void フレームワークのコールバックを静的化候補にしない()
    {
        const string source = """
            using System.Runtime.Serialization;

            internal sealed class Worker
            {
                private int _count;

                public void Touch() => _count++;

                [OnDeserialized]
                public void Callback(StreamingContext context)
                {
                }
            }
            """;

        AssertNone(new 静的メンバー候補規則(), source, "OOP103");
    }

    private static void 通常の外部型属性は静的クラス候補を抑制しない()
    {
        const string source = """
            [System.Obsolete]
            internal class Formatter
            {
                public int Version() => 1;
            }
            """;

        AssertSingleRule(new 静的クラス候補規則(), source, "OOP104");
    }

    private static void 通常の外部メソッド属性は静的メンバー候補を抑制しない()
    {
        const string source = """
            internal sealed class Worker
            {
                private int _count;

                public void Touch() => _count++;

                [System.Obsolete]
                public int Version() => 1;
            }
            """;

        AssertSingleRule(new 静的メンバー候補規則(), source, "OOP103");
    }

    private static void 属性付き型を静的クラス候補にしない()
    {
        const string source = """
            [System.Serializable]
            internal class SnapshotFormatter
            {
                public int Version() => 1;
            }
            """;

        AssertNone(new 静的クラス候補規則(), source, "OOP104");
    }

    private static void SerializableTypeRemainsSealingCandidate()
    {
        const string source = """
            [System.Serializable]
            internal class Snapshot
            {
            }
            """;

        AssertSingleRule(new 封止候補規則(), source, "OOP102");
    }

    private static void 変更可能な静的プロパティは警告になる()
    {
        const string source = """
            internal static class GlobalState
            {
                public static int Count { get; set; }
            }
            """;

        AssertSingle(
            new 状態保持静的設計規則(),
            source,
            "OOP105",
            DesignDiagnosticSeverity.Warning
        );
    }

    private static void 読み取り専用宣言の可変コレクションは警告になる()
    {
        const string source = """
            using System.Collections.Generic;

            internal static class GlobalState
            {
                private static readonly List<int> Items = new();

                public static void Add(int value) => Items.Add(value);
            }
            """;

        AssertSingle(
            new 状態保持静的設計規則(),
            source,
            "OOP105",
            DesignDiagnosticSeverity.Warning
        );
    }

    private static void スレッドローカルフィールドは許可する()
    {
        const string source = """
            internal static class PerThreadState
            {
                [System.ThreadStatic]
                private static int _count;

                public static void Increment() => _count++;
            }
            """;

        AssertNone(new 状態保持静的設計規則(), source, "OOP105");
    }

    private static void AssertNone(I解析規則 rule, string source, string ruleId)
    {
        var diagnostics = rule.解析する(TestProjectFactory.解析文脈を作る(source)).ToArray();
        if (diagnostics.Length == 0)
        {
            return;
        }

        var found = string.Join(", ", diagnostics.Select(item => item.Rule.Id));
        throw new InvalidOperationException($"Expected no {ruleId} diagnostic, found [{found}].");
    }

    private static void AssertSingleRule(I解析規則 rule, string source, string ruleId)
    {
        var diagnostics = rule.解析する(TestProjectFactory.解析文脈を作る(source)).ToArray();
        if (diagnostics.Length == 1 && diagnostics[0].Rule.Id == ruleId)
        {
            return;
        }

        var found = string.Join(", ", diagnostics.Select(item => item.Rule.Id));
        throw new InvalidOperationException($"Expected one {ruleId} diagnostic, found [{found}].");
    }

    private static void AssertSingle(
        状態保持静的設計規則 rule,
        string source,
        string ruleId,
        DesignDiagnosticSeverity severity
    )
    {
        var diagnostics = rule.解析する(TestProjectFactory.解析文脈を作る(source)).ToArray();
        if (
            diagnostics.Length != 1
            || diagnostics[0].Rule.Id != ruleId
            || diagnostics[0].Severity != severity
        )
        {
            var found = string.Join(
                ", ",
                diagnostics.Select(item => $"{item.Rule.Id}:{item.Severity}")
            );
            throw new InvalidOperationException(
                $"Expected one {ruleId}:{severity} diagnostic, found [{found}]."
            );
        }
    }
}
