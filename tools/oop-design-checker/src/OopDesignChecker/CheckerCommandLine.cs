using OopDesignChecker.Application;
using OopDesignChecker.Output;

namespace OopDesignChecker;

/*
{
  "責務": {
    "CheckerCommandLine": "CLI引数から表示設定を持つ書き込み器を構成してアプリケーションを実行します。"
  }
}
*/
public static class CheckerCommandLine
{
    /*
    {
      "責務": {
        "Run": "CLI引数から表示設定を持つ書き込み器を構成してアプリケーションを実行します。"
      },
      "処理": [
        "バージョン要求またはCLI引数を解釈します。",
        "text専用の設定を分離して書き込み器を生成します。",
        "解析アプリケーションを実行して終了コードを返します。"
      ],
      "引数": {
        "args": "利用者が指定したCLI引数です。"
      },
      "戻り値": "成功は0、診断による失敗は1、入力や実行の失敗は2です。"
    }
    */
    public static int Run(IReadOnlyList<string> args)
    {
        if (args.Count == 1 && args[0] is "--version" or "-V")
        {
            Console.WriteLine(ProductVersionResolver.Resolve(typeof(CheckerCommandLine).Assembly));
            return 0;
        }

        var 要求された言語 = コマンドライン引数解析器.ResolveRequestedLanguage(args);
        var 解析結果 = コマンドライン引数解析器.Parse(args);
        if (!解析結果.IsSuccess)
        {
            Console.Error.WriteLine(解析結果.ErrorMessage);
            Console.Error.WriteLine(コマンドライン引数解析器.Usage(要求された言語));
            return 2;
        }

        var 引数設定 = 解析結果.Options!;
        // 詳細表示と言語はtext writerを構成するときだけ渡します。
        var 診断書き込み器 =
            引数設定.OutputFormat == 診断出力形式.Text
                ? 診断書き込み器生成器.テキストを作る(
                    引数設定.OutputPath,
                    引数設定.Language,
                    引数設定.Verbose
                )
                : 診断書き込み器生成器.Create(引数設定.OutputFormat, 引数設定.OutputPath);
        var アプリケーション = new チェッカーアプリケーション(診断書き込み器);
        return アプリケーション.Run(引数設定);
    }
}
