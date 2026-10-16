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
            .Where(rule => !disabledRules.Contains(rule.規則情報.Id))
            .ToArray();

        Iプロジェクト読み込み器 loader = プロジェクト読み込み器生成器.生成する(
            configuration.IgnoredPaths
        );
        var projects = loader.プロジェクト群を読み込む(fullTargetPath, cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();

        var engine = new 解析実行器(rules);
        var diagnostics = engine.解析する(projects, cancellationToken);
        var failureThreshold = failureThresholdOverride ?? configuration.FailureThreshold;

        return new CheckerRunResult(diagnostics, failureThreshold, loadedConfiguration.Path)
        {
            Configuration = configuration,
        };
    }
}
