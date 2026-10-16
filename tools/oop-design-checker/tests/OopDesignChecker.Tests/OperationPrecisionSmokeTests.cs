using OopDesignChecker.Core;
using OopDesignChecker.Rules;

namespace OopDesignChecker.Tests;

internal static class 操作精度スモークテスト
{
    public static void 実行する()
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
        var 文一覧 = 繰り返し文を作る(130, "value += 1;");
        var ソース = $$"""
            internal sealed class Processor
            {
                public int Run()
                {
                    var value = 0;
            {{字下げする(文一覧)}}
                    return value;
                }
            }
            """;

        単一診断を確認する(ソース);
    }

    private static void 複雑で深く入れ子の主処理は警告になる()
    {
        var 分岐 = string.Join(
            Environment.NewLine,
            Enumerable
                .Range(0, 18)
                .Select(位置 =>
                    $"if (value > {位置}){Environment.NewLine}{{{Environment.NewLine}    value--;{Environment.NewLine}}}"
                )
        );
        var ソース = $$"""
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
            {{字下げする(分岐, 20)}}
                                }
                            }
                        }
                    }

                    return value;
                }
            }
            """;

        単一診断を確認する(ソース);
    }

    private static void コメントが多くても単純な処理は許可する()
    {
        var コメント一覧 = string.Join(
            Environment.NewLine,
            Enumerable.Range(0, 140).Select(位置 => $"// phase note {位置}")
        );
        var ソース = $$"""
            internal sealed class Processor
            {
                public int Run()
                {
            {{字下げする(コメント一覧)}}
                    return 1;
                }
            }
            """;

        診断がないことを確認する(ソース);
    }

    private static void 外部フレームワーク型の処理は許可する()
    {
        var 文一覧 = 繰り返し文を作る(130, "value += 1;");
        var ソース = $$"""
            internal class ProcessorException : System.Exception
            {
                public int BuildCode()
                {
                    var value = 0;
            {{字下げする(文一覧)}}
                    return value;
                }
            }
            """;

        診断がないことを確認する(ソース);
    }

    private static void 通常の外部属性は大きな処理の診断を抑制しない()
    {
        var 文一覧 = 繰り返し文を作る(130, "value += 1;");
        var ソース = $$"""
            internal sealed class Processor
            {
                [System.Obsolete]
                public int Run()
                {
                    var value = 0;
            {{字下げする(文一覧)}}
                    return value;
                }
            }
            """;

        単一診断を確認する(ソース);
    }

    private static void シリアライズコールバックを過大な主処理としない()
    {
        var 文一覧 = 繰り返し文を作る(130, "value += 1;");
        var ソース = $$"""
            using System.Runtime.Serialization;

            internal sealed class Processor
            {
                [OnDeserialized]
                public void Restore(StreamingContext context)
                {
                    var value = 0;
            {{字下げする(文一覧)}}
                    _ = value;
                }
            }
            """;

        診断がないことを確認する(ソース);
    }

    private static void モジュール初期化子を過大な主処理としない()
    {
        var 文一覧 = 繰り返し文を作る(130, "value += 1;");
        var ソース = $$"""
            using System.Runtime.CompilerServices;

            internal static class Processor
            {
                [ModuleInitializer]
                internal static void Initialize()
                {
                    var value = 0;
            {{字下げする(文一覧)}}
                    _ = value;
                }
            }
            """;

        診断がないことを確認する(ソース);
    }

    private static void 呼び出されるprivate補助処理は許可する()
    {
        var 文一覧 = 繰り返し文を作る(130, "value += 1;");
        var ソース = $$"""
            internal sealed class Processor
            {
                public int Run() => BuildValue();

                private int BuildValue()
                {
                    var value = 0;
            {{字下げする(文一覧)}}
                    return value;
                }
            }
            """;

        診断がないことを確認する(ソース);
    }

    private static void 入れ子のローカル関数で外側処理を過大評価しない()
    {
        var 文一覧 = 繰り返し文を作る(130, "value += 1;");
        var ソース = $$"""
            internal sealed class Processor
            {
                public int Run()
                {
                    int BuildValue()
                    {
                        var value = 0;
            {{字下げする(文一覧, 24)}}
                        return value;
                    }

                    return BuildValue();
                }
            }
            """;

        診断がないことを確認する(ソース);
    }

    private static string 繰り返し文を作る(int 件数, string 文) =>
        string.Join(Environment.NewLine, Enumerable.Repeat(文, 件数));

    private static string 字下げする(string テキスト, int 空白数 = 12)
    {
        var 接頭辞 = new string(' ', 空白数);
        return string.Join(
            Environment.NewLine,
            テキスト.Split(Environment.NewLine).Select(行 => 接頭辞 + 行)
        );
    }

    private static void 単一診断を確認する(string ソース)
    {
        var 診断一覧 = new 過大主処理規則()
            .解析する(テスト用プロジェクト作成器.解析文脈を作る(ソース))
            .ToArray();
        if (
            診断一覧.Length != 1
            || 診断一覧[0].Rule.Id != "OOP201"
            || 診断一覧[0].Severity != DesignDiagnosticSeverity.Warning
        )
        {
            var 検出内容 = string.Join(
                ", ",
                診断一覧.Select(項目 => $"{項目.Rule.Id}:{項目.Severity}")
            );
            throw new InvalidOperationException(
                $"Expected one OOP201:Warning diagnostic, found [{検出内容}]."
            );
        }
    }

    private static void 診断がないことを確認する(string ソース)
    {
        var 診断一覧 = new 過大主処理規則()
            .解析する(テスト用プロジェクト作成器.解析文脈を作る(ソース))
            .ToArray();
        if (診断一覧.Length == 0)
        {
            return;
        }

        var 検出内容 = string.Join(", ", 診断一覧.Select(項目 => 項目.Rule.Id));
        throw new InvalidOperationException($"Expected no OOP201 diagnostic, found [{検出内容}].");
    }
}
