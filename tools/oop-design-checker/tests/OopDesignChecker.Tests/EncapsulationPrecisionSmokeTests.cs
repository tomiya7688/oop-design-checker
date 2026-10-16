using OopDesignChecker.Core;
using OopDesignChecker.Rules;

namespace OopDesignChecker.Tests;

internal static class EncapsulationPrecisionSmokeTests
{
    public static void Run()
    {
        FrameworkFacingPublicTypeIsAllowed();
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

    private static void FrameworkFacingPublicTypeIsAllowed()
    {
        const string source = """
            public sealed class SampleException : System.Exception
            {
            }
            """;

        AssertNone(new 過剰可視性規則(), source, "OOP101");
    }

    private static void カプセル化規則でシリアル化データ保持型を許可する()
    {
        const string source = """
            using System.Collections.Generic;

            [System.Serializable]
            internal sealed class Snapshot
            {
                public int Version;
                public List<int> Values { get; set; } = new();
            }
            """;

        AssertNone(new カプセル化漏出規則(), source, "OOP106");
    }

    private static void 非公開入れ子型の可変フィールドを公開扱いにしない()
    {
        const string source = """
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

        AssertNone(new カプセル化漏出規則(), source, "OOP106");
    }

    private static void 非公開入れ子型のコレクションと設定子を公開扱いにしない()
    {
        const string source = """
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

        AssertNone(new カプセル化漏出規則(), source, "OOP106");
    }

    private static void 非公開型内の非公開入れ子型を公開扱いにしない()
    {
        const string source = """
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

        AssertNone(new カプセル化漏出規則(), source, "OOP106");
    }

    private static void 公開可変フィールドを危険として検出する()
    {
        const string source = """
            public struct PublicState
            {
                public int Value;
            }
            """;

        AssertSingle(new カプセル化漏出規則(), source, "OOP106", DesignDiagnosticSeverity.Danger);
    }

    private static void 内部可変フィールドを危険として検出する()
    {
        const string source = """
            internal struct InternalState
            {
                public int Value;
            }
            """;

        AssertSingle(new カプセル化漏出規則(), source, "OOP106", DesignDiagnosticSeverity.Danger);
    }

    private static void 保護された入れ子型の可変フィールドを危険として検出する()
    {
        const string source = """
            public class Owner
            {
                protected struct State
                {
                    public int Value;
                }

                protected State Create() => new();
            }
            """;

        AssertSingle(new カプセル化漏出規則(), source, "OOP106", DesignDiagnosticSeverity.Danger);
    }

    private static void 非公開保護入れ子型の可変フィールドを危険として検出する()
    {
        const string source = """
            public class Owner
            {
                private protected struct State
                {
                    public int Value;
                }

                private protected State Create() => new();
            }
            """;

        AssertSingle(new カプセル化漏出規則(), source, "OOP106", DesignDiagnosticSeverity.Danger);
    }

    private static void メッセージ型への外部書き込みを許可する()
    {
        const string source = """
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

        AssertNone(new 外部状態操作過剰規則(), source, "OOP108");
    }

    private static void 検証付き公開設定子で不変条件を迂回しない()
    {
        const string source = """
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

        AssertNone(new 不変条件迂回規則(), source, "OOP107");
    }

    private static void 未検証の公開設定子による不変条件迂回を検出する()
    {
        const string source = """
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

        AssertSingle(new 不変条件迂回規則(), source, "OOP107", DesignDiagnosticSeverity.Danger);
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

    private static void AssertSingle(
        I解析規則 rule,
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
