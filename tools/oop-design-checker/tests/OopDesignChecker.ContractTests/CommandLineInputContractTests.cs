using OopDesignChecker.Application;
using Xunit;

namespace OopDesignChecker.ContractTests;

/*
{
  "責務": {
    "CLI契約テスト": "CLIのオプション境界と不正なソリューション入力の終了契約を確認します。"
  },
  "フィールド": {
    "値必須オプション群": "値が必要なCLIオプションです。",
    "既知オプション群": "値として消費しないCLIオプションです。",
    "表示言語群": "検証する日英の表示言語です。",
    "不正なSLNX群": "拒否すべきXMLと参照の入力例です。"
  }
}
*/
public sealed partial class CLI契約テスト
{
    private static readonly string[] 値必須オプション群 =
    [
        "--config",
        "--fail-on",
        "--format",
        "--output",
        "--language",
    ];
    private static readonly string[] 既知オプション群 =
    [
        "--config",
        "--fail-on",
        "--format",
        "--output",
        "--language",
        "--warnings-as-errors",
        "--verbose",
        "--help",
        "-h",
        "--version",
        "-V",
    ];

    public static IEnumerable<object[]> 値オプションと後続オプションの組み合わせ =>
        from 値オプション in 値必須オプション群
        from 後続オプション in 既知オプション群
        select new object[] { 値オプション, 後続オプション };

    /*
    {
      "責務": {
        "後続オプションを値として消費しない": "次の既知オプションを値として消費せず、値省略時と同じエラーにすることを確認します。"
      },
      "処理": [
        "次の既知オプションを値として消費せず、値省略時と同じエラーにすることを確認します。"
      ],
      "引数": {
        "値オプション": "値を必要とするオプションです。",
        "後続オプション": "直後に指定する既知オプションです。"
      },
      "戻り値": []
    }
    */
    [Theory]
    [MemberData(nameof(値オプションと後続オプションの組み合わせ))]
    public void 後続オプションを値として消費しない(string 値オプション, string 後続オプション)
    {
        var 省略結果 = コマンドライン引数解析器.Parse([値オプション]);
        var 結果 = コマンドライン引数解析器.Parse([値オプション, 後続オプション]);

        Assert.False(結果.IsSuccess);
        Assert.Null(結果.Options);
        Assert.Equal(省略結果.ErrorMessage, 結果.ErrorMessage);
    }

    /*
    {
      "責務": {
        "ハイフン付きのパス値を受け入れる": "ハイフンで始まる通常のファイル名と、相対パスで指定したオプション同名ファイルを受け入れます。"
      },
      "処理": [
        "ハイフンで始まる通常のファイル名と、相対パスで指定したオプション同名ファイルを受け入れます。"
      ],
      "引数": {
        "パス": "ハイフンで始まるファイル名または相対パスです。"
      },
      "戻り値": []
    }
    */
    [Theory]
    [InlineData("-report.json")]
    [InlineData("./--verbose")]
    public void ハイフン付きのパス値を受け入れる(string パス)
    {
        var 出力結果 = コマンドライン引数解析器.Parse(["--output", パス]);
        var 設定結果 = コマンドライン引数解析器.Parse(["--config", パス]);

        Assert.True(出力結果.IsSuccess);
        Assert.Equal(Path.GetFullPath(パス), 出力結果.Options!.OutputPath);
        Assert.True(設定結果.IsSuccess);
        Assert.Equal(パス, 設定結果.Options!.ConfigurationPath);
    }

    private static readonly string[] 表示言語群 = ["ja", "en"];
    private static readonly string[] 不正なSLNX群 =
    [
        "",
        "<Solution>",
        "<Other />",
        "<Solution />",
        "<Solution><Project /></Solution>",
        "<Solution><Project Path=\" \" /></Solution>",
        "<Solution><Project Path=\"missing.csproj\" /></Solution>",
        "<Solution><Project Path=\"bad&#0;name.csproj\" /></Solution>",
        "<Solution xmlns=\"urn:unsupported\"><Project Path=\"a.csproj\" /></Solution>",
        "<Solution><Other><Project Path=\"a.csproj\" /></Other></Solution>",
        "<!DOCTYPE Solution [<!ENTITY project \"a.csproj\">]><Solution><Project Path=\"&project;\" /></Solution>",
    ];

    public static IEnumerable<object[]> 不正なSLNXと言語の組み合わせ =>
        from 内容 in 不正なSLNX群
        from 言語 in 表示言語群
        select new object[] { 内容, 言語 };

    /*
    {
      "責務": {
        "不正なSLNXの失敗を利用者へ返す": "不正なSLNXを未処理例外にせず、対象パスを含むエラーと終了コード2に変換することを確認します。"
      },
      "処理": [
        "不正なSLNXを未処理例外にせず、対象パスを含むエラーと終了コード2に変換することを確認します。"
      ],
      "引数": {
        "内容": "無効なSLNX内容です。",
        "言語": "エラーの表示言語です。"
      },
      "戻り値": []
    }
    */
    [Theory]
    [MemberData(nameof(不正なSLNXと言語の組み合わせ))]
    public void 不正なSLNXの失敗を利用者へ返す(string 内容, string 言語)
    {
        一時ディレクトリで実行する(ルート =>
        {
            var 対象 = Path.Combine(ルート, "invalid.slnx");
            File.WriteAllText(対象, 内容);

            var 結果 = CLIを実行する([対象, "--language", 言語]);

            Assert.Equal(2, 結果.終了コード);
            Assert.Contains(対象, 結果.エラー, StringComparison.Ordinal);
            Assert.DoesNotContain(
                "System.Xml.XmlException:",
                結果.エラー,
                StringComparison.Ordinal
            );
        });
    }

    /*
    {
      "責務": {
        "CLIでコンパイルエラーの省略を通知する": "上限を超えるコンパイルエラーをCLIが表示言語に応じて省略通知することを確認します。"
      },
      "処理": [
        "上限を超えるコンパイルエラーをCLIが表示言語に応じて省略通知することを確認します。"
      ],
      "引数": {
        "言語": "エラーの表示言語です。",
        "期待通知": "省略を示す通知の一部です。"
      },
      "戻り値": []
    }
    */
    [Theory]
    [InlineData("ja", "1件を省略")]
    [InlineData("en", "1 omitted")]
    public void CLIでコンパイルエラーの省略を通知する(string 言語, string 期待通知)
    {
        一時ディレクトリで実行する(ルート =>
        {
            var フィールド群 = string.Join(
                "\n",
                Enumerable.Range(0, 21).Select(番号 => $"Unknown{番号} value{番号};")
            );
            var 対象 = Path.Combine(ルート, "Broken.cs");
            File.WriteAllText(対象, $"internal sealed class Sample {{ {フィールド群} }}");
            var 結果 = CLIを実行する([対象, "--language", 言語]);
            Assert.Equal(2, 結果.終了コード);
            Assert.Contains(期待通知, 結果.エラー, StringComparison.Ordinal);
        });
    }
}
