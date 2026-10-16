using OopDesignChecker.Core;
using OopDesignChecker.Rules;

namespace OopDesignChecker.Tests;

internal static class 静的設計精度スモークテスト
{
    public static void 実行する()
    {
        通常の外部型属性で可視性診断を抑制しない();
        外部契約型をシール候補にしない();
        外部基底型の契約メソッドを静的化候補にしない();
        通常の外部型属性は静的クラス候補を抑制しない();
        通常の外部メソッド属性は静的メンバー候補を抑制しない();
        フレームワークのコールバックを静的化候補にしない();
        属性付き型を静的クラス候補にしない();
        シリアル化型をシール候補として扱う();
        変更可能な静的プロパティは警告になる();
        読み取り専用宣言の可変コレクションは警告になる();
        スレッドローカルフィールドは許可する();
    }

    private static void 通常の外部型属性で可視性診断を抑制しない()
    {
        const string ソース = """
            [System.Obsolete]
            public class Base
            {
            }

            internal class Child : Base
            {
            }
            """;

        単一規則の診断を確認する(new 過剰可視性規則(), ソース, "OOP101");
    }

    private static void 外部契約型をシール候補にしない()
    {
        const string ソース = """
            internal class SampleException : System.Exception
            {
            }
            """;

        診断がないことを確認する(new 封止候補規則(), ソース, "OOP102");
    }

    private static void 外部基底型の契約メソッドを静的化候補にしない()
    {
        const string ソース = """
            internal class SampleException : System.Exception
            {
                private int _count;

                public void Touch() => _count++;
                public string Describe() => "sample";
            }
            """;

        診断がないことを確認する(new 静的メンバー候補規則(), ソース, "OOP103");
    }

    private static void フレームワークのコールバックを静的化候補にしない()
    {
        const string ソース = """
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

        診断がないことを確認する(new 静的メンバー候補規則(), ソース, "OOP103");
    }

    private static void 通常の外部型属性は静的クラス候補を抑制しない()
    {
        const string ソース = """
            [System.Obsolete]
            internal class Formatter
            {
                public int Version() => 1;
            }
            """;

        単一規則の診断を確認する(new 静的クラス候補規則(), ソース, "OOP104");
    }

    private static void 通常の外部メソッド属性は静的メンバー候補を抑制しない()
    {
        const string ソース = """
            internal sealed class Worker
            {
                private int _count;

                public void Touch() => _count++;

                [System.Obsolete]
                public int Version() => 1;
            }
            """;

        単一規則の診断を確認する(new 静的メンバー候補規則(), ソース, "OOP103");
    }

    private static void 属性付き型を静的クラス候補にしない()
    {
        const string ソース = """
            [System.Serializable]
            internal class SnapshotFormatter
            {
                public int Version() => 1;
            }
            """;

        診断がないことを確認する(new 静的クラス候補規則(), ソース, "OOP104");
    }

    private static void シリアル化型をシール候補として扱う()
    {
        const string ソース = """
            [System.Serializable]
            internal class Snapshot
            {
            }
            """;

        単一規則の診断を確認する(new 封止候補規則(), ソース, "OOP102");
    }

    private static void 変更可能な静的プロパティは警告になる()
    {
        const string ソース = """
            internal static class GlobalState
            {
                public static int Count { get; set; }
            }
            """;

        単一診断を確認する(
            new 状態保持静的設計規則(),
            ソース,
            "OOP105",
            DesignDiagnosticSeverity.Warning
        );
    }

    private static void 読み取り専用宣言の可変コレクションは警告になる()
    {
        const string ソース = """
            using System.Collections.Generic;

            internal static class GlobalState
            {
                private static readonly List<int> Items = new();

                public static void Add(int value) => Items.Add(value);
            }
            """;

        単一診断を確認する(
            new 状態保持静的設計規則(),
            ソース,
            "OOP105",
            DesignDiagnosticSeverity.Warning
        );
    }

    private static void スレッドローカルフィールドは許可する()
    {
        const string ソース = """
            internal static class PerThreadState
            {
                [System.ThreadStatic]
                private static int _count;

                public static void Increment() => _count++;
            }
            """;

        診断がないことを確認する(new 状態保持静的設計規則(), ソース, "OOP105");
    }

    private static void 診断がないことを確認する(I解析規則 規則, string ソース, string 規則ID)
    {
        var 診断一覧 = 規則.解析する(テスト用プロジェクト作成器.解析文脈を作る(ソース)).ToArray();
        if (診断一覧.Length == 0)
        {
            return;
        }

        var 検出内容 = string.Join(", ", 診断一覧.Select(項目 => 項目.Rule.Id));
        throw new InvalidOperationException(
            $"Expected no {規則ID} diagnostic, found [{検出内容}]."
        );
    }

    private static void 単一規則の診断を確認する(I解析規則 規則, string ソース, string 規則ID)
    {
        var 診断一覧 = 規則.解析する(テスト用プロジェクト作成器.解析文脈を作る(ソース)).ToArray();
        if (診断一覧.Length == 1 && 診断一覧[0].Rule.Id == 規則ID)
        {
            return;
        }

        var 検出内容 = string.Join(", ", 診断一覧.Select(項目 => 項目.Rule.Id));
        throw new InvalidOperationException(
            $"Expected one {規則ID} diagnostic, found [{検出内容}]."
        );
    }

    private static void 単一診断を確認する(
        状態保持静的設計規則 規則,
        string ソース,
        string 規則ID,
        DesignDiagnosticSeverity 重要度
    )
    {
        var 診断一覧 = 規則.解析する(テスト用プロジェクト作成器.解析文脈を作る(ソース)).ToArray();
        if (診断一覧.Length != 1 || 診断一覧[0].Rule.Id != 規則ID || 診断一覧[0].Severity != 重要度)
        {
            var 検出内容 = string.Join(
                ", ",
                診断一覧.Select(項目 => $"{項目.Rule.Id}:{項目.Severity}")
            );
            throw new InvalidOperationException(
                $"Expected one {規則ID}:{重要度} diagnostic, found [{検出内容}]."
            );
        }
    }
}
