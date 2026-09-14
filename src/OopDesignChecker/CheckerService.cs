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
        DesignDiagnosticSeverity? failureThresholdOverride = null
    )
    {
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

        var disabledRules = configuration.DisabledRules.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var rules = RuleCatalog
            .CreateDefault()
            .Where(rule => !disabledRules.Contains(rule.Descriptor.Id))
            .ToArray();

        var loader = new CSharpProjectLoader(configuration.IgnoredPaths);
        var project = loader.Load(fullTargetPath);
        var engine = new AnalysisEngine(rules);
        var diagnostics = engine.Analyze(project);
        var failureThreshold = failureThresholdOverride ?? configuration.FailureThreshold;

        return new CheckerRunResult(diagnostics, failureThreshold, loadedConfiguration.Path);
    }
}
