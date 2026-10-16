using System.Reflection;
using System.Reflection.Emit;
using OopDesignChecker;

namespace OopDesignChecker.Tests;

internal static class 製品版情報解決テスト
{
    public static void 実行する()
    {
        製品情報バージョンからビルド情報を除く();
        製品情報バージョンがない場合にアセンブリ版を使う();
        両方のバージョンがない場合に不明を返す();
        GUIのバージョンにGUIアセンブリを使う();
    }

    private static void 製品情報バージョンからビルド情報を除く()
    {
        var アセンブリ = テスト用アセンブリを作る(
            "InformationalVersionFixture",
            new Version(2, 3, 4, 0),
            "5.6.7-preview.2+build.9"
        );

        バージョンを確認する("5.6.7-preview.2", ProductVersionResolver.Resolve(アセンブリ));
    }

    private static void 製品情報バージョンがない場合にアセンブリ版を使う()
    {
        var アセンブリ = テスト用アセンブリを作る(
            "AssemblyVersionFixture",
            new Version(8, 9, 10, 11)
        );

        バージョンを確認する("8.9.10", ProductVersionResolver.Resolve(アセンブリ));
    }

    private static void 両方のバージョンがない場合に不明を返す() =>
        バージョンを確認する("unknown", ProductVersionResolver.バージョンを解決する(null, null));

    private static void GUIのバージョンにGUIアセンブリを使う()
    {
        var 期待値 = ProductVersionResolver.Resolve(
            typeof(OopDesignChecker.Gui.GUI起動処理).Assembly
        );
        var 元の標準出力 = Console.Out;
        using var 出力 = new StringWriter();
        try
        {
            Console.SetOut(出力);
            OopDesignChecker.Gui.GUI起動処理.Main(["--version"]);
        }
        finally
        {
            Console.SetOut(元の標準出力);
        }

        バージョンを確認する(期待値, 出力.ToString().Trim());
    }

    private static AssemblyBuilder テスト用アセンブリを作る(
        string 名前,
        Version? アセンブリバージョン,
        string? 製品情報バージョン = null
    )
    {
        var アセンブリ名 = new AssemblyName(名前) { Version = アセンブリバージョン };
        var アセンブリ = AssemblyBuilder.DefineDynamicAssembly(
            アセンブリ名,
            AssemblyBuilderAccess.Run
        );

        if (製品情報バージョン is not null)
        {
            var 属性コンストラクター =
                typeof(AssemblyInformationalVersionAttribute).GetConstructor([typeof(string)]);
            if (属性コンストラクター is null)
            {
                throw new InvalidOperationException(
                    "AssemblyInformationalVersionAttribute constructor was not found."
                );
            }

            アセンブリ.SetCustomAttribute(
                new CustomAttributeBuilder(属性コンストラクター, [製品情報バージョン])
            );
        }

        return アセンブリ;
    }

    private static void バージョンを確認する(string 期待値, string 実際の値)
    {
        if (!string.Equals(期待値, 実際の値, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"Expected product version '{期待値}', found '{実際の値}'."
            );
        }
    }
}
