using OopDesignChecker.Analysis;
using OopDesignChecker.Localization;
using OopDesignChecker.Output;

namespace OopDesignChecker.Application;

/*
{
  "責務": {
    "チェッカーアプリケーション": "解析結果を構成済みの書き込み器へ渡し、設定通知と終了コードを管理します。"
  }
}
*/
internal sealed class チェッカーアプリケーション
{
    private readonly 診断書き込み器 _診断書き込み器;

    public チェッカーアプリケーション(診断書き込み器 診断書き込み器)
    {
        _診断書き込み器 = 診断書き込み器;
    }

    /*
    {
      "責務": {
        "Run": "解析結果を構成済みの書き込み器へ渡し、設定通知と終了コードを管理します。"
      },
      "処理": [
            "対象を解析して診断一覧を構成済みの書き込み器へ渡します。",
            "詳細表示時の設定通知を適切な出力先へ書き込みます。",
            "診断の失敗閾値または実行エラーから終了コードを決めます。"
      ],
      "引数": {
        "options": "解析対象とCLIの実行設定です。"
      },
      "戻り値": "成功は0、診断による失敗は1、入力や実行の失敗は2です。"
    }
    */
    public int Run(コマンドライン引数 options)
    {
        try
        {
            var 解析結果 = CheckerService.Analyze(
                options.TargetPath,
                options.ConfigurationPath,
                options.FailureThreshold
            );

            _診断書き込み器.Write(解析結果.Diagnostics);

            if (options.Verbose && 解析結果.ConfigurationPath is not null)
            {
                var 出力先 =
                    options.OutputFormat == 診断出力形式.Text && options.OutputPath is null
                        ? Console.Out
                        : Console.Error;
                出力先.WriteLine(
                    LocalizedText.LoadedConfig(options.Language, 解析結果.ConfigurationPath)
                );
            }

            return 解析結果.ShouldFail ? 1 : 0;
        }
        catch (Exception 例外)
            when (例外 is IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            var 説明文 = 例外 is 解析入力例外 入力失敗
                ? 入力失敗.説明文を選ぶ(options.Language)
                : LocalizedText.AnalysisFailed(options.Language, 例外.Message);
            Console.Error.WriteLine(説明文);
            return 2;
        }
    }
}
