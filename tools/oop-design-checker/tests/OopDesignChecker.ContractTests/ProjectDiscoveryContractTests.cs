using OopDesignChecker.Analysis;
using Xunit;

namespace OopDesignChecker.ContractTests;

/*
{
  "責務": {
    "プロジェクト探索契約テスト": "ディレクトリ指定のプロジェクト探索と明示ソリューションの境界を確認します。"
  },
  "フィールド": []
}
*/
public sealed class プロジェクト探索契約テスト
{
    /*
    {
      "責務": {
        "直下のプロジェクトがあっても配下を探索する": "直下と配下のプロジェクトをすべて解析し、参照で読み込まれたプロジェクトも重複診断しないことを確認します。"
      },
      "処理": [
        "直下と配下のプロジェクトをすべて解析し、参照で読み込まれたプロジェクトも重複診断しないことを確認します。"
      ],
      "引数": {
        "直下数": "直下へ配置するプロジェクト数です。",
        "参照する": "配下のプロジェクトを参照する場合にtrueです。"
      },
      "戻り値": []
    }
    */
    [Theory]
    [InlineData(1, false)]
    [InlineData(2, false)]
    [InlineData(1, true)]
    [InlineData(2, true)]
    public void 直下のプロジェクトがあっても配下を探索する(int 直下数, bool 参照する)
    {
        using var 環境 = new 一時解析環境();
        環境.プロジェクトを作る(
            "modules/Feature/Feature.csproj",
            "namespace Feature; internal sealed class Sample { public int Value; }"
        );
        for (var 位置 = 0; 位置 < 直下数; 位置++)
        {
            環境.プロジェクトを作る(
                $"Root{位置}.csproj",
                $"namespace Root{位置}; internal sealed class Sample {{ public int Value; }}",
                参照先: 参照する ? "modules/Feature/Feature.csproj" : null
            );
        }
        var プロジェクト群 = new CSharpプロジェクト読み込み器().プロジェクト群を読み込む(
            環境.ルート,
            TestContext.Current.CancellationToken
        );
        Assert.Equal(直下数 + 1, プロジェクト群.Count);
        var アセンブリ名群 = プロジェクト群
            .Select(プロジェクト => プロジェクト.コンパイル結果.AssemblyName)
            .ToArray();
        Assert.Equal(直下数 + 1, アセンブリ名群.Distinct().Count());
        Assert.Contains("Feature", アセンブリ名群);

        var 診断群 = CheckerService
            .Analyze(環境.ルート, cancellationToken: TestContext.Current.CancellationToken)
            .Diagnostics.Where(診断 => 診断.Rule.Id == "OOP106")
            .ToArray();
        Assert.Equal(直下数 + 1, 診断群.Length);
        Assert.Equal(直下数 + 1, 診断群.Select(診断 => 診断.Location.FilePath).Distinct().Count());
    }

    /*
    {
      "責務": {
        "設定と組み込みの除外を探索へ適用する": "探索対象から設定で除外した配下と、ビルド生成物の不正プロジェクトを除くことを確認します。"
      },
      "処理": [
        "探索対象から設定で除外した配下と、ビルド生成物の不正プロジェクトを除くことを確認します。"
      ],
      "引数": [],
      "戻り値": []
    }
    */
    [Fact]
    public void 設定と組み込みの除外を探索へ適用する()
    {
        using var 環境 = new 一時解析環境();
        環境.プロジェクトを作る("Root.csproj", "internal sealed class Root { }");
        foreach (var 除外先 in new[] { "modules/Feature", "bin", "obj", ".git", ".vs" })
        {
            環境.ファイルを書く($"{除外先}/Broken.csproj", "not XML");
        }
        var 結果 = new CSharpプロジェクト読み込み器(["modules/**"]).プロジェクト群を読み込む(
            環境.ルート,
            TestContext.Current.CancellationToken
        );
        Assert.Single(結果);
        Assert.Equal("Root", 結果[0].コンパイル結果.AssemblyName);
    }

    /*
    {
      "責務": {
        "SLNXで明示したプロジェクト集合を優先する": "ディレクトリ直下のソリューションを優先し、列挙されていない配下プロジェクトを追加しないことを確認します。"
      },
      "処理": [
        "ディレクトリ直下のソリューションを優先し、列挙されていない配下プロジェクトを追加しないことを確認します。"
      ],
      "引数": [],
      "戻り値": []
    }
    */
    [Fact]
    public void SLNXで明示したプロジェクト集合を優先する()
    {
        using var 環境 = new 一時解析環境();
        環境.プロジェクトを作る("Root.csproj", "internal sealed class Root { }");
        環境.ファイルを書く("modules/Other/Broken.csproj", "not XML");
        var ソリューション = 環境.ファイルを書く(
            "Selected.slnx",
            "<Solution><Folder Name=\"/Group/\"><Project Path=\"Root.csproj\" /></Folder></Solution>"
        );
        var 読み込み器 = new CSharpプロジェクト読み込み器();
        Assert.Single(
            読み込み器.プロジェクト群を読み込む(環境.ルート, TestContext.Current.CancellationToken)
        );
        Assert.Single(
            読み込み器.プロジェクト群を読み込む(
                ソリューション,
                TestContext.Current.CancellationToken
            )
        );
    }
}
