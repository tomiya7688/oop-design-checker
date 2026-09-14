using OopDesignChecker.Analysis;
using OopDesignChecker.Core;
using OopDesignChecker.Output;

namespace OopDesignChecker.Application;

internal sealed class CheckerApplication
{
    private readonly IProjectLoader _projectLoader;
    private readonly AnalysisEngine _analysisEngine;
    private readonly IDiagnosticWriter _diagnosticWriter;

    public CheckerApplication(
        IProjectLoader projectLoader,
        AnalysisEngine analysisEngine,
        IDiagnosticWriter diagnosticWriter)
    {
        _projectLoader = projectLoader;
        _analysisEngine = analysisEngine;
        _diagnosticWriter = diagnosticWriter;
    }

    public int Run(CommandLineOptions options)
    {
        if (!Directory.Exists(options.TargetPath) && !File.Exists(options.TargetPath))
        {
            Console.Error.WriteLine($"Target does not exist: {options.TargetPath}");
            return 2;
        }

        try
        {
            var project = _projectLoader.Load(options.TargetPath);
            var diagnostics = _analysisEngine.Analyze(project);

            _diagnosticWriter.Write(diagnostics, options.Verbose);

            var hasErrors = diagnostics.Any(diagnostic => diagnostic.Severity == DesignDiagnosticSeverity.Error);
            var hasWarnings = diagnostics.Any(diagnostic => diagnostic.Severity == DesignDiagnosticSeverity.Warning);

            return hasErrors || (options.WarningsAsErrors && hasWarnings) ? 1 : 0;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            Console.Error.WriteLine($"Analysis failed: {exception.Message}");
            return 2;
        }
    }
}
