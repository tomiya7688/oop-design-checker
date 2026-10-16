using OopDesignChecker.Core;
using OopDesignChecker.Rules;

namespace OopDesignChecker.Tests;

internal static class カプセル化精度スモークテスト
{
    public static void 実行する()
    {
        フレームワーク向け公開型を許可する();
        カプセル化規則でシリアル化データ保持型を許可する();
        非公開入れ子型の可変フィールドを公開扱いにしない();
        非公開入れ子型のコレクションと設定子を公開扱いにしない();
        非公開型内の非公開入れ子型を公開扱いにしない();
        公開可変フィールドを危険として検出する();
        内部可変フィールドを危険として検出する();
        保護された入れ子型の可変フィールドを危険として検出する();
        非公開保護入れ子型の可変フィールドを危険として検出する();
        メッセージ型への外部書き込みを許可する();
        検証付き公開設定子で不変条件を迂回しない();
        未検証の公開設定子による不変条件迂回を検出する();
    }

    private static void フレームワーク向け公開型を許可する()
    {
        const string ソース = """
            public sealed class SampleException : System.Exception
            {
            }
            """;

        診断がないことを確認する(new 過剰可視性規則(), ソース, "OOP101");
    }

    private static void カプセル化規則でシリアル化データ保持型を許可する()
    {
        const string ソース = """
            using System.Collections.Generic;

            [System.Serializable]
            internal sealed class Snapshot
            {
                public int Version;
                public List<int> Values { get; set; } = new();
            }
            """;

        診断がないことを確認する(new カプセル化漏出規則(), ソース, "OOP106");
    }

    private static void 非公開入れ子型の可変フィールドを公開扱いにしない()
    {
        const string ソース = """
            public sealed class Owner
            {
                private struct State
                {
                    public int X;
                    public int Y;
                }

                private State _state;

                public void Set(int value)
                {
                    _state.X = value;
                }

                public int Read() => _state.X + _state.Y;
            }
            """;

        診断がないことを確認する(new カプセル化漏出規則(), ソース, "OOP106");
    }

    private static void 非公開入れ子型のコレクションと設定子を公開扱いにしない()
    {
        const string ソース = """
            using System.Collections.Generic;

            public sealed class Owner
            {
                private sealed class State
                {
                    private readonly List<int> _values = new();

                    public List<int> Values => _values;

                    public int Count { get; set; }
                }

                private readonly State _state = new();

                public int Count => _state.Count;
            }
            """;

        診断がないことを確認する(new カプセル化漏出規則(), ソース, "OOP106");
    }

    private static void 非公開型内の非公開入れ子型を公開扱いにしない()
    {
        const string ソース = """
            public sealed class Owner
            {
                private sealed class Container
                {
                    private struct State
                    {
                        public int Value;
                    }

                    private State _state;

                    public int Read() => _state.Value;
                }

                private readonly Container _container = new();

                public int Read() => _container.Read();
            }
            """;

        診断がないことを確認する(new カプセル化漏出規則(), ソース, "OOP106");
    }

    private static void 公開可変フィールドを危険として検出する()
    {
        const string ソース = """
            public struct PublicState
            {
                public int Value;
            }
            """;

        単一診断を確認する(
            new カプセル化漏出規則(),
            ソース,
            "OOP106",
            DesignDiagnosticSeverity.Danger
        );
    }

    private static void 内部可変フィールドを危険として検出する()
    {
        const string ソース = """
            internal struct InternalState
            {
                public int Value;
            }
            """;

        単一診断を確認する(
            new カプセル化漏出規則(),
            ソース,
            "OOP106",
            DesignDiagnosticSeverity.Danger
        );
    }

    private static void 保護された入れ子型の可変フィールドを危険として検出する()
    {
        const string ソース = """
            public class Owner
            {
                protected struct State
                {
                    public int Value;
                }

                protected State Create() => new();
            }
            """;

        単一診断を確認する(
            new カプセル化漏出規則(),
            ソース,
            "OOP106",
            DesignDiagnosticSeverity.Danger
        );
    }

    private static void 非公開保護入れ子型の可変フィールドを危険として検出する()
    {
        const string ソース = """
            public class Owner
            {
                private protected struct State
                {
                    public int Value;
                }

                private protected State Create() => new();
            }
            """;

        単一診断を確認する(
            new カプセル化漏出規則(),
            ソース,
            "OOP106",
            DesignDiagnosticSeverity.Danger
        );
    }

    private static void メッセージ型への外部書き込みを許可する()
    {
        const string ソース = """
            internal sealed class UserCreatedMessage
            {
                public string UserId { get; set; } = string.Empty;
                public string Name { get; set; } = string.Empty;
                public string Email { get; set; } = string.Empty;
            }

            internal sealed class MessageFactory
            {
                public void Fill(UserCreatedMessage message)
                {
                    message.UserId = "1";
                    message.Name = "name";
                    message.Email = "mail@example.com";
                }
            }
            """;

        診断がないことを確認する(new 外部状態操作過剰規則(), ソース, "OOP108");
    }

    private static void 検証付き公開設定子で不変条件を迂回しない()
    {
        const string ソース = """
            using System;

            internal sealed class Account
            {
                private int _balance;

                public int Balance
                {
                    get => _balance;
                    set
                    {
                        if (value < 0)
                        {
                            throw new ArgumentOutOfRangeException(nameof(value));
                        }

                        _balance = value;
                    }
                }

                public void ChangeBalance(int value)
                {
                    if (value < 0)
                    {
                        throw new ArgumentOutOfRangeException(nameof(value));
                    }

                    Balance = value;
                }
            }
            """;

        診断がないことを確認する(new 不変条件迂回規則(), ソース, "OOP107");
    }

    private static void 未検証の公開設定子による不変条件迂回を検出する()
    {
        const string ソース = """
            using System;

            internal sealed class Account
            {
                public int Balance { get; set; }

                public void ChangeBalance(int value)
                {
                    if (value < 0)
                    {
                        throw new ArgumentOutOfRangeException(nameof(value));
                    }

                    Balance = value;
                }
            }
            """;

        単一診断を確認する(
            new 不変条件迂回規則(),
            ソース,
            "OOP107",
            DesignDiagnosticSeverity.Danger
        );
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

    private static void 単一診断を確認する(
        I解析規則 規則,
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
