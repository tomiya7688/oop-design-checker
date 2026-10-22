using OopDesignChecker.Localization;
using Xunit;

namespace OopDesignChecker.ContractTests;

/*
{
  "責務": {"CLI契約テスト": "詳細表示がtextの本文だけに作用し、機械可読形式と出力先を変えないことを確認します。"}
}
*/
public sealed partial class CLI契約テスト
{
    /*
    {
      "責務": {"機械可読出力は詳細表示で変わらない": "CLIからverboseを切り替えても正規の本文が同じであることを確認します。"},
      "処理": ["同じ対象を通常表示と詳細表示で解析します。", "本文、終了コード、設定通知の出力先を比較します。"],
      "引数": {"形式": "JSON、SARIF、GitHub注釈のいずれかです。", "言語": "CLIの表示言語です。", "ファイル出力": "ファイルへ診断を出力するかを表します。"},
      "戻り値": []
    }
    */
    [Theory]
    [InlineData("json", "ja", false)]
    [InlineData("json", "en", false)]
    [InlineData("sarif", "ja", false)]
    [InlineData("sarif", "en", false)]
    [InlineData("github", "ja", false)]
    [InlineData("github", "en", false)]
    [InlineData("json", "en", true)]
    [InlineData("sarif", "en", true)]
    public void 機械可読出力は詳細表示で変わらない(string 形式, string 言語, bool ファイル出力)
    {
        using var 環境 = new 一時解析環境();
        var 引数 = 出力比較用引数を作る(環境, 形式, 言語, ファイル出力);
        var 通常 = CLIを実行する(引数);
        var 通常本文 = ファイル出力 ? File.ReadAllText(引数[^1]) : 通常.出力;
        var 詳細 = CLIを実行する([.. 引数, "--verbose"]);
        var 詳細本文 = ファイル出力 ? File.ReadAllText(引数[^1]) : 詳細.出力;

        Assert.Equal(1, 通常.終了コード);
        Assert.Equal(通常.終了コード, 詳細.終了コード);
        Assert.Contains("OOP106", 通常本文, StringComparison.Ordinal);
        Assert.Equal(通常本文, 詳細本文);
        Assert.Equal(通常.出力, 詳細.出力);
        設定通知を確認する(環境, 言語, 通常.エラー, 詳細.エラー);
    }

    /*
    {
      "責務": {"textの詳細表示だけに見出しを追加する": "textの見出しと設定通知を、言語と出力先に応じて維持します。"},
      "処理": ["通常表示と詳細表示の診断を比較します。", "見出しと設定通知が意図した出力先にあることを確認します。"],
      "引数": {"言語": "CLIの表示言語です。", "ファイル出力": "ファイルへ診断を出力するかを表します。"},
      "戻り値": []
    }
    */
    [Theory]
    [InlineData("ja", false)]
    [InlineData("en", false)]
    [InlineData("ja", true)]
    [InlineData("en", true)]
    public void textの詳細表示だけに見出しを追加する(string 言語, bool ファイル出力)
    {
        using var 環境 = new 一時解析環境();
        var 引数 = 出力比較用引数を作る(環境, "text", 言語, ファイル出力);
        var 通常 = CLIを実行する(引数);
        var 通常本文 = ファイル出力 ? File.ReadAllText(引数[^1]) : 通常.出力;
        var 詳細 = CLIを実行する([.. 引数, "--verbose"]);
        var 詳細本文 = ファイル出力 ? File.ReadAllText(引数[^1]) : 詳細.出力;

        Assert.Equal(1, 通常.終了コード);
        Assert.Equal(通常.終了コード, 詳細.終了コード);
        // 通常表示の各行を維持したうえで、詳細表示だけに字下げした規則名を加えます。
        Assert.DoesNotContain($"{Environment.NewLine}  ", 通常本文, StringComparison.Ordinal);
        Assert.Contains($"{Environment.NewLine}  ", 詳細本文, StringComparison.Ordinal);
        foreach (
            var 行 in 通常本文.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries)
        )
        {
            Assert.Contains(行, 詳細本文, StringComparison.Ordinal);
        }

        設定通知を確認する(環境, 言語, 通常.エラー, ファイル出力 ? 詳細.エラー : 詳細.出力);
        if (!ファイル出力)
        {
            Assert.Empty(詳細.エラー);
        }
    }

    /*
    {
      "責務": {"出力比較用引数を作る": "外部設定に依存しない同一入力でCLIの出力を比較できるようにします。"},
      "処理": ["危険診断がある単体ソースを作ります。", "形式、言語、明示設定、必要な出力先を指定します。"],
      "引数": {"環境": "一時ファイルを所有する環境です。", "形式": "出力形式です。", "言語": "表示言語です。", "ファイル出力": "ファイルへ書き込む場合にtrueです。"},
      "戻り値": "通常表示を実行するCLI引数です。"
    }
    */
    private static string[] 出力比較用引数を作る(
        一時解析環境 環境,
        string 形式,
        string 言語,
        bool ファイル出力
    )
    {
        var ソース = 環境.ファイルを書く(
            "Danger.cs",
            "internal sealed class Danger { public int Value; }"
        );
        string[] 引数 =
        [
            ソース,
            "--config",
            Path.Combine(環境.ルート, "oop-design-checker.json"),
            "--format",
            形式,
            "--language",
            言語,
            "--fail-on",
            "danger",
        ];
        return ファイル出力
            ? [.. 引数, "--output", Path.Combine(環境.ルート, "diagnostics.txt")]
            : 引数;
    }

    /*
    {
      "責務": {"設定通知を確認する": "通常表示には設定通知を出さず、詳細表示には選択言語の通知を出すことを確認します。"},
      "処理": ["通常表示のエラー出力と詳細表示の設定通知を確認します。"],
      "引数": {"環境": "設定ファイルを所有する環境です。", "言語": "表示言語です。", "通常エラー": "通常表示の標準エラーです。", "詳細通知": "詳細表示の通知を含む出力です。"},
      "戻り値": []
    }
    */
    private static void 設定通知を確認する(
        一時解析環境 環境,
        string 言語,
        string 通常エラー,
        string 詳細通知
    )
    {
        Assert.Empty(通常エラー);
        Assert.Contains(
            LocalizedText.LoadedConfig(
                言語 == "ja" ? UserInterfaceLanguage.Japanese : UserInterfaceLanguage.English,
                Path.Combine(環境.ルート, "oop-design-checker.json")
            ),
            詳細通知,
            StringComparison.Ordinal
        );
    }
}
