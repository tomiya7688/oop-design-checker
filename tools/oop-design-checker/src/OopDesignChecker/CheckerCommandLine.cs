using OopDesignChecker.Application;
using OopDesignChecker.Output;

namespace OopDesignChecker;

public static class CheckerCommandLine
{
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
        var 診断書き込み器 = 診断書き込み器生成器.Create(
            引数設定.OutputFormat,
            引数設定.OutputPath,
            引数設定.Language
        );
        var アプリケーション = new チェッカーアプリケーション(診断書き込み器);
        return アプリケーション.Run(引数設定);
    }
}
