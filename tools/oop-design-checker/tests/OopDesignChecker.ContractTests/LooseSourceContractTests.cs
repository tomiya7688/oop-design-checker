using OopDesignChecker.Analysis;
using Xunit;

namespace OopDesignChecker.ContractTests;

/*
{
  "責務": {
    "単体ソース契約テスト": "単体ソースと明示プロジェクトの解析コンテキストを区別します。"
  },
  "フィールド": []
}
*/
public sealed class 単体ソース契約テスト
{
    /*
    {
      "責務": {
        "周辺プロジェクトから独立して解析する": "単体C#ファイルの解析が周辺プロジェクトの種類や破損に影響されないことを確認します。"
      },
      "処理": [
        "単体C#ファイルの解析が周辺プロジェクトの種類や破損に影響されないことを確認します。"
      ],
      "引数": {
        "周辺パス": "無関係なcsprojの配置先です。",
        "不正XML": "周辺csprojを壊す場合にtrueです。"
      },
      "戻り値": []
    }
    */
    [Theory]
    [InlineData("App.csproj", false)]
    [InlineData("nested/App.csproj", false)]
    [InlineData("Broken.csproj", true)]
    [InlineData("nested/Broken.csproj", true)]
    public void 周辺プロジェクトから独立して解析する(string 周辺パス, bool 不正XML)
    {
        using var 環境 = new 一時解析環境();
        var 対象 = 環境.ファイルを書く(
            "Sample.cs",
            "public class Parent { public int Read() => 1; } public class Child : Parent { }"
        );
        環境.ファイルを書く("Other.cs", "not C# source");
        環境.ファイルを書く(
            周辺パス,
            不正XML
                ? "<Project>"
                : "<Project><PropertyGroup><OutputType>Exe</OutputType></PropertyGroup></Project>"
        );

        var 結果 = new CSharpプロジェクト読み込み器().読み込む(
            対象,
            TestContext.Current.CancellationToken
        );
        Assert.False(結果.アプリケーションである);
        Assert.Single(結果.構文木群);
        Assert.DoesNotContain(
            CheckerService
                .Analyze(対象, cancellationToken: TestContext.Current.CancellationToken)
                .Diagnostics,
            診断 => 診断.Rule.Id is "OOP101" or "OOP102"
        );
    }

    /*
    {
      "責務": {
        "明示した実行プロジェクトの種別を保持する": "明示した実行プロジェクトではMSBuildの出力種別を引き続き利用することを確認します。"
      },
      "処理": [
        "明示した実行プロジェクトではMSBuildの出力種別を引き続き利用することを確認します。"
      ],
      "引数": [],
      "戻り値": []
    }
    */
    [Fact]
    public void 明示した実行プロジェクトの種別を保持する()
    {
        using var 環境 = new 一時解析環境();
        var 対象 = 環境.プロジェクトを作る(
            "App.csproj",
            "internal static class Entry { static void Main() { } }",
            アプリケーション: true
        );
        Assert.True(
            new CSharpプロジェクト読み込み器()
                .読み込む(対象, TestContext.Current.CancellationToken)
                .アプリケーションである
        );
    }

    /*
    {
      "責務": {
        "コンパイルエラーの表示件数を明示する": "コンパイルエラーの表示上限と省略件数を検査し、正常なソースの成功も確認します。"
      },
      "処理": [
        "コンパイルエラーの表示上限と省略件数を検査し、正常なソースの成功も確認します。"
      ],
      "引数": {
        "エラー数": "生成するコンパイルエラーの件数です。"
      },
      "戻り値": []
    }
    */
    [Theory]
    [InlineData(0)]
    [InlineData(20)]
    [InlineData(21)]
    [InlineData(37)]
    public void コンパイルエラーの表示件数を明示する(int エラー数)
    {
        using var 環境 = new 一時解析環境();
        var フィールド群 = string.Join(
            "\n",
            Enumerable.Range(0, エラー数).Select(番号 => $"Unknown{番号} value{番号};")
        );
        var 対象 = 環境.ファイルを書く(
            "Compilation.cs",
            $"internal sealed class Sample {{ {フィールド群} }}"
        );
        var 読み込み器 = new CSharpプロジェクト読み込み器();
        if (エラー数 == 0)
        {
            Assert.Single(
                読み込み器.読み込む(対象, TestContext.Current.CancellationToken).構文木群
            );
            return;
        }
        var 例外 = Assert.ThrowsAny<InvalidOperationException>(() =>
            読み込み器.読み込む(対象, TestContext.Current.CancellationToken)
        );
        Assert.Equal(
            Math.Min(20, エラー数),
            例外.Message.Split("error CS0246", StringSplitOptions.None).Length - 1
        );
        if (エラー数 > 20)
        {
            Assert.Contains(
                $"showing 20 of {エラー数} errors; {エラー数 - 20} omitted",
                例外.Message,
                StringComparison.Ordinal
            );
        }
        else
        {
            Assert.DoesNotContain("omitted", 例外.Message, StringComparison.Ordinal);
        }
    }
}
