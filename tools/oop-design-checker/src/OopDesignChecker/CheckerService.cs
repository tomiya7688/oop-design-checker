using OopDesignChecker.Analysis;
using OopDesignChecker.Configuration;
using OopDesignChecker.Core;
using OopDesignChecker.Rules;

namespace OopDesignChecker;

public static class CheckerService
{
    public static CheckerRunResult Analyze(
        string targetPath,
        string? configurationPath = null,
        DesignDiagnosticSeverity? failureThresholdOverride = null,
        CancellationToken cancellationToken = default
    )
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (!Directory.Exists(targetPath) && !File.Exists(targetPath))
        {
            throw new InvalidOperationException($"Target does not exist: {targetPath}");
        }

        var fullTargetPath = Path.GetFullPath(targetPath);
        var loadedConfiguration = CheckerConfigurationLoader.Load(
            fullTargetPath,
            configurationPath
        );
        var configuration = loadedConfiguration.Configuration;
        cancellationToken.ThrowIfCancellationRequested();

        var disabledRules = configuration.DisabledRules.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var rules = RuleCatalog
            .CreateDefault(configuration)
            .Where(rule => !disabledRules.Contains(rule.Descriptor.Id))
            .ToArray();

        IProjectLoader loader = ProjectLoaderFactory.Create(configuration.IgnoredPaths);
        var projects = loader.LoadProjects(fullTargetPath, cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();

        var engine = new AnalysisEngine(rules);
        var diagnostics = engine.Analyze(projects, cancellationToken);
        var failureThreshold = failureThresholdOverride ?? configuration.FailureThreshold;

        return new CheckerRunResult(diagnostics, failureThreshold, loadedConfiguration.Path)
        {
            Configuration = configuration,
        };
    }
}
