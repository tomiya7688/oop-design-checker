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

        var 対象の絶対パス = Path.GetFullPath(targetPath);
        var 読込済み設定 = チェッカー設定読み込み器.Load(対象の絶対パス, configurationPath);
        var 設定 = 読込済み設定.Configuration;
        cancellationToken.ThrowIfCancellationRequested();

        var 無効な規則群 = 設定.DisabledRules.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var 規則群 = 規則一覧.既定の規則一覧を作る(設定)
            .Where(rule => !無効な規則群.Contains(rule.規則情報.Id))
            .ToArray();

        Iプロジェクト読み込み器 読み込み器 = プロジェクト読み込み器生成器.生成する(
            設定.IgnoredPaths
        );
        var プロジェクト群 = 読み込み器.プロジェクト群を読み込む(対象の絶対パス, cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();

        var 解析実行器 = new 解析実行器(規則群);
        var 診断群 = 解析実行器.解析する(プロジェクト群, cancellationToken);
        var 失敗基準 = failureThresholdOverride ?? 設定.FailureThreshold;

        return new CheckerRunResult(診断群, 失敗基準, 読込済み設定.Path) { Configuration = 設定 };
    }
}
