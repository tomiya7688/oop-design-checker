using OopDesignChecker.Core;
using OopDesignChecker.Rules;

namespace OopDesignChecker.Tests;

internal static class PolymorphismPrecisionSmokeTests
{
    public static void Run()
    {
        共通利用がない同形クラスを許可する();
        無関係な型の分岐を許可する();
        マーカーインターフェースを不要な抽象としない();
        実装が1つのpublicインターフェースを拡張点とする();
    }

    private static void 共通利用がない同形クラスを許可する()
    {
        const string source = """
            internal sealed class CacheEntry
            {
                public void Open() { }
                public void Close() { }
            }

            internal sealed class Window
            {
                public void Open() { }
                public void Close() { }
            }

            internal sealed class Connection
            {
                public void Open() { }
                public void Close() { }
            }
            """;

        診断がないことを確認する(new 共通抽象不足規則(), source, "OOP001");
    }

    private static void 無関係な型の分岐を許可する()
    {
        const string source = """
            internal sealed class Command
            {
            }

            internal sealed class Snapshot
            {
            }

            internal sealed class Handler
            {
                public void Handle(object value)
                {
                    if (value is Command)
                    {
                    }
                    else if (value is Snapshot)
                    {
                    }
                }
            }
            """;

        診断がないことを確認する(new 型分岐ポリモーフィズム規則(), source, "OOP002");
    }

    private static void マーカーインターフェースを不要な抽象としない()
    {
        const string source = """
            internal interface IMarker
            {
            }

            internal sealed class Tagged : IMarker
            {
            }
            """;

        診断がないことを確認する(new UnnecessaryAbstractionRule(), source, "OOP003");
    }

    private static void 実装が1つのpublicインターフェースを拡張点とする()
    {
        const string source = """
            public interface IPlugin
            {
                void Run();
            }

            internal sealed class BuiltInPlugin : IPlugin
            {
                public void Run() { }
            }
            """;

        診断がないことを確認する(new UnnecessaryAbstractionRule(), source, "OOP003");
    }

    private static void 診断がないことを確認する(I解析規則 rule, string source, string ruleId)
    {
        var diagnostics = rule.解析する(TestProjectFactory.解析文脈を作る(source)).ToArray();
        if (diagnostics.Length == 0)
        {
            return;
        }

        var found = string.Join(", ", diagnostics.Select(item => item.Rule.Id));
        throw new InvalidOperationException($"Expected no {ruleId} diagnostic, found [{found}].");
    }
}
