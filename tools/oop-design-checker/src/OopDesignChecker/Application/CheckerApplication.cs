using OopDesignChecker.Analysis;
using OopDesignChecker.Localization;
using OopDesignChecker.Output;

namespace OopDesignChecker.Application;

internal sealed class チェッカーアプリケーション
{
    private readonly 診断書き込み器 _診断書き込み器;

    public チェッカーアプリケーション(診断書き込み器 診断書き込み器)
    {
        _診断書き込み器 = 診断書き込み器;
    }

    public int Run(コマンドライン引数 options)
    {
        try
        {
            var 解析結果 = CheckerService.Analyze(
                options.TargetPath,
                options.ConfigurationPath,
                options.FailureThreshold
            );

            _診断書き込み器.Write(解析結果.Diagnostics, options.Verbose);

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
            var 説明文 = 例外 is AnalysisFailureException 解析失敗
                ? 解析失敗.GetLocalizedMessage(options.Language)
                : LocalizedText.AnalysisFailed(options.Language, 例外.Message);
            Console.Error.WriteLine(説明文);
            return 2;
        }
    }
}
