using OopDesignChecker.Core;
using OopDesignChecker.Rules;

namespace OopDesignChecker.Tests;

internal static class 多態性精度スモークテスト
{
    public static void 実行する()
    {
        共通利用がない同形クラスを許可する();
        無関係な型の分岐を許可する();
        マーカーインターフェースを不要な抽象としない();
        実装が1つのpublicインターフェースを拡張点とする();
    }

    private static void 共通利用がない同形クラスを許可する()
    {
        const string ソース = """
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

        診断がないことを確認する(new 共通抽象不足規則(), ソース, "OOP001");
    }

    private static void 無関係な型の分岐を許可する()
    {
        const string ソース = """
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

        診断がないことを確認する(new 型分岐ポリモーフィズム規則(), ソース, "OOP002");
    }

    private static void マーカーインターフェースを不要な抽象としない()
    {
        const string ソース = """
            internal interface IMarker
            {
            }

            internal sealed class Tagged : IMarker
            {
            }
            """;

        診断がないことを確認する(new 不要な抽象規則(), ソース, "OOP003");
    }

    private static void 実装が1つのpublicインターフェースを拡張点とする()
    {
        const string ソース = """
            public interface IPlugin
            {
                void Run();
            }

            internal sealed class BuiltInPlugin : IPlugin
            {
                public void Run() { }
            }
            """;

        診断がないことを確認する(new 不要な抽象規則(), ソース, "OOP003");
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
}
