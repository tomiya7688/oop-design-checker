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
                Console.WriteLine($"Config: {result.ConfigurationPath}");
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
            Console.Error.WriteLine($"Analysis failed: {exception.Message}");
            return 2;
        }
    }
}
