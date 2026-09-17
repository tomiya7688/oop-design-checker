using OopDesignChecker.Localization;
using OopDesignChecker.Output;

namespace OopDesignChecker.Application;

internal sealed class CheckerApplication
{
    private readonly IDiagnosticWriter _diagnosticWriter;

    public CheckerApplication(IDiagnosticWriter diagnosticWriter)
    {
        _diagnosticWriter = diagnosticWriter;
    }

    public int Run(CommandLineOptions options)
    {
        try
        {
            var result = CheckerService.Analyze(
                options.TargetPath,
                options.ConfigurationPath,
                options.FailureThreshold
            );

            _diagnosticWriter.Write(result.Diagnostics, options.Verbose);

            if (options.Verbose && result.ConfigurationPath is not null)
            {
                var writer =
                    options.OutputFormat == DiagnosticOutputFormat.Text
                    && options.OutputPath is null
                        ? Console.Out
                        : Console.Error;
                writer.WriteLine(
                    UserInterfaceText.Select(
                        options.Language,
                        $"設定: {result.ConfigurationPath}",
                        $"Config: {result.ConfigurationPath}"
                    )
                );
            }

            return result.ShouldFail ? 1 : 0;
        }
        catch (Exception exception)
            when (exception
                    is IOException
                        or UnauthorizedAccessException
                        or InvalidOperationException
            )
        {
            Console.Error.WriteLine(
                UserInterfaceText.Select(
                    options.Language,
                    $"解析に失敗しました: {exception.Message}",
                    $"Analysis failed: {exception.Message}"
                )
            );
            return 2;
        }
    }
}
