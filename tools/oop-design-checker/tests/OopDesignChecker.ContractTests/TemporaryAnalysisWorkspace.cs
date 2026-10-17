using System.Diagnostics;
using System.Xml.Linq;

namespace OopDesignChecker.ContractTests;

/*
{
  "責務": {
    "一時解析環境": "プロジェクト読み込み契約のために生成した一時ファイルと復元処理を所有します。"
  },
  "フィールド": {
    "ルート": "このテストが所有する一時ディレクトリです。",
    "復元待機上限ミリ秒": "復元が停止した場合の待機上限です。"
  }
}
*/
internal sealed class 一時解析環境 : IDisposable
{
    // 復元のハングでテスト全体が停止しないよう、外部パッケージのない復元を1分で打ち切ります。
    private const int 復元待機上限ミリ秒 = 60_000;
    public string ルート { get; } =
        Path.Combine(Path.GetTempPath(), "oop-input-contract", Guid.NewGuid().ToString("N"));

    /*
    {
      "責務": {
        "一時解析環境": "専用ディレクトリと空の設定を作り、親ディレクトリの設定からテストを独立させます。"
      },
      "処理": [
        "専用ディレクトリと空の設定を作り、親ディレクトリの設定からテストを独立させます。"
      ],
      "引数": [],
      "戻り値": []
    }
    */
    public 一時解析環境()
    {
        Directory.CreateDirectory(ルート);
        ファイルを書く("oop-design-checker.json", "{}");
    }

    /*
    {
      "責務": {
        "ファイルを書く": "テストが所有する一時ディレクトリへファイルを書きます。"
      },
      "処理": [
        "テストが所有する一時ディレクトリへファイルを書きます。"
      ],
      "引数": {
        "相対パス": "所有する一時ディレクトリからのファイルパスです。",
        "内容": "書き込む内容です。"
      },
      "戻り値": "作成したファイルの絶対パスです。"
    }
    */
    public string ファイルを書く(string 相対パス, string 内容)
    {
        var パス = Path.Combine(ルート, 相対パス);
        Directory.CreateDirectory(Path.GetDirectoryName(パス)!);
        File.WriteAllText(パス, 内容);
        return パス;
    }

    /*
    {
      "責務": {
        "プロジェクトを作る": "独立したソース一覧を持つSDK形式のプロジェクトを作ります。"
      },
      "処理": [
        "独立したソース一覧を持つSDK形式のプロジェクトを作ります。"
      ],
      "引数": {
        "相対パス": "作成するcsprojの相対パスです。",
        "ソース": "専用のソースファイル内容です。",
        "アプリケーション": "実行プロジェクトを作る場合にtrueです。",
        "参照先": "csprojからのプロジェクト参照です。"
      },
      "戻り値": "復元済みcsprojの絶対パスです。"
    }
    */
    public string プロジェクトを作る(
        string 相対パス,
        string ソース,
        bool アプリケーション = false,
        string? 参照先 = null
    )
    {
        var 設定 = new XElement(
            "Project",
            new XAttribute("Sdk", "Microsoft.NET.Sdk"),
            new XElement(
                "PropertyGroup",
                new XElement("TargetFramework", "net10.0"),
                new XElement("EnableDefaultCompileItems", "false"),
                new XElement("OutputType", アプリケーション ? "Exe" : "Library")
            ),
            new XElement(
                "ItemGroup",
                new XElement(
                    "Compile",
                    new XAttribute("Include", Path.GetFileNameWithoutExtension(相対パス) + ".cs")
                )
            )
        );
        if (参照先 is not null)
        {
            設定.Add(
                new XElement(
                    "ItemGroup",
                    new XElement("ProjectReference", new XAttribute("Include", 参照先))
                )
            );
        }
        var プロジェクトパス = ファイルを書く(相対パス, 設定.ToString());
        ファイルを書く(Path.ChangeExtension(相対パス, ".cs"), ソース);
        依存関係を復元する(プロジェクトパス);
        return プロジェクトパス;
    }

    /*
    {
      "責務": {
        "依存関係を復元する": "外部パッケージを持たないテスト用プロジェクトのMSBuild資産を準備します。"
      },
      "処理": [
        "外部パッケージを持たないテスト用プロジェクトのMSBuild資産を準備します。"
      ],
      "引数": {
        "プロジェクトパス": "復元する一時csprojの絶対パスです。"
      },
      "戻り値": [],
      "エラー": "復元失敗と60秒のタイムアウトをテスト失敗として報告します。"
    }
    */
    private static void 依存関係を復元する(string プロジェクトパス)
    {
        var 起動設定 = new ProcessStartInfo("dotnet")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        foreach (
            var 引数 in new[]
            {
                "restore",
                プロジェクトパス,
                "--ignore-failed-sources",
                "--verbosity",
                "quiet",
            }
        )
        {
            起動設定.ArgumentList.Add(引数);
        }
        using var 復元処理 =
            Process.Start(起動設定)
            ?? throw new InvalidOperationException("dotnet restoreを起動できませんでした。");
        var 出力 = 復元処理.StandardOutput.ReadToEndAsync();
        var エラー = 復元処理.StandardError.ReadToEndAsync();
        if (!復元処理.WaitForExit(復元待機上限ミリ秒))
        {
            復元処理.Kill(entireProcessTree: true);
            throw new TimeoutException(
                "テスト用プロジェクトの復元が60秒以内に終了しませんでした。"
            );
        }
        if (復元処理.ExitCode != 0)
        {
            throw new InvalidOperationException(
                $"復元に失敗しました: {出力.GetAwaiter().GetResult()} {エラー.GetAwaiter().GetResult()}"
            );
        }
    }

    /*
    {
      "責務": {
        "Dispose": "このインスタンスが生成したGUID付き一時ディレクトリを削除します。"
      },
      "処理": [
        "このインスタンスが生成したGUID付き一時ディレクトリを削除します。"
      ],
      "引数": [],
      "戻り値": []
    }
    */
    public void Dispose() => Directory.Delete(ルート, recursive: true);
}
