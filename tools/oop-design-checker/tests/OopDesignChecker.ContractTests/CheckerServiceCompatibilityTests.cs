using OopDesignChecker.Analysis;
using OopDesignChecker.Core;
using Xunit;

namespace OopDesignChecker.ContractTests;

/*
{
  "責務": "公開static解析APIの引数とエラー優先順を維持します。",
  "フィールド": []
}
*/
public sealed class 公開解析API互換性テスト
{
    /*
    {
      "責務": "設定探索より対象の存在確認を優先し、元の対象をエラーに含めます。",
      "処理": ["存在しない対象と設定を名前付き引数で渡します。", "対象側のエラー説明を確認します。"],
      "引数": [],
      "戻り値": []
    }
    */
    [Fact]
    public void 対象がなければ設定を探索しない()
    {
        var 対象 = Path.Combine(
            Path.GetTempPath(),
            "missing-analysis-" + Guid.NewGuid().ToString("N")
        );
        var 例外 = Assert.Throws<AnalysisFailureException>(() =>
            CheckerService.Analyze(
                targetPath: 対象,
                configurationPath: "must-not-read.json",
                failureThresholdOverride: DesignDiagnosticSeverity.Warning,
                cancellationToken: TestContext.Current.CancellationToken
            )
        );
        Assert.Equal(AnalysisFailureKind.Target, 例外.Kind);
        Assert.Equal(対象, 例外.Path);
        Assert.Equal($"Target does not exist: {対象}", 例外.Message);
    }

    /*
    {
      "責務": "対象の確認よりも事前キャンセルを優先します。",
      "処理": ["キャンセル済み通知と無効な対象を渡します。", "元の通知を持つキャンセル例外を確認します。"],
      "引数": [],
      "戻り値": []
    }
    */
    [Fact]
    public void 対象確認より事前キャンセルを優先する()
    {
        using var 通知元 = CancellationTokenSource.CreateLinkedTokenSource(
            TestContext.Current.CancellationToken
        );
        通知元.Cancel();
        var 例外 = Assert.Throws<OperationCanceledException>(() =>
            CheckerService.Analyze(
                targetPath: null!,
                configurationPath: "must-not-read.json",
                cancellationToken: 通知元.Token
            )
        );
        Assert.Equal(通知元.Token, 例外.CancellationToken);
    }
}
