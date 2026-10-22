using OopDesignChecker.Analysis;
using OopDesignChecker.Localization;
using Xunit;

namespace OopDesignChecker.ContractTests;

public sealed class 解析失敗分類契約テスト
{
    [Fact]
    public void 対象と設定と読込とコンパイルを別種別で返す()
    {
        using var 環境 = new 一時失敗環境();

        var 正常ソース = 環境.書く(
            "Valid.cs",
            "internal sealed class Valid { private int _value; public int Value => _value; }"
        );
        var 未対応 = 環境.書く("unsupported.txt", "text");
        var 壊れたソース = 環境.書く(
            "Broken.cs",
            "internal sealed class Broken { UnknownType Value; }"
        );
        var 壊れたSLNX = 環境.書く("Broken.slnx", "<Solution><Project");
        var 壊れた設定 = 環境.書く("broken-config.json", "{ not-json");
        var 存在しない設定 = Path.Combine(環境.ルート, "missing-config.json");

        var 対象失敗 = Assert.Throws<AnalysisFailureException>(() =>
            CheckerService.Analyze(未対応, cancellationToken: TestContext.Current.CancellationToken)
        );
        Assert.Equal(AnalysisFailureKind.Target, 対象失敗.Kind);
        Assert.Contains(".csproj", 対象失敗.Message, StringComparison.Ordinal);

        var 設定不在 = Assert.Throws<AnalysisFailureException>(() =>
            CheckerService.Analyze(
                正常ソース,
                存在しない設定,
                cancellationToken: TestContext.Current.CancellationToken
            )
        );
        Assert.Equal(AnalysisFailureKind.Configuration, 設定不在.Kind);
        Assert.Contains("Configuration file does not exist", 設定不在.Message, StringComparison.Ordinal);
        Assert.Contains(
            "設定ファイル",
            設定不在.GetLocalizedMessage(UserInterfaceLanguage.Japanese),
            StringComparison.Ordinal
        );

        var 設定不正 = Assert.Throws<AnalysisFailureException>(() =>
            CheckerService.Analyze(
                正常ソース,
                壊れた設定,
                cancellationToken: TestContext.Current.CancellationToken
            )
        );
        Assert.Equal(AnalysisFailureKind.Configuration, 設定不正.Kind);

        var 読込失敗 = Assert.Throws<AnalysisFailureException>(() =>
            CheckerService.Analyze(
                壊れたSLNX,
                cancellationToken: TestContext.Current.CancellationToken
            )
        );
        Assert.Equal(AnalysisFailureKind.ProjectLoad, 読込失敗.Kind);
        Assert.Contains(
            "ソリューションファイルを読み込めませんでした",
            読込失敗.GetLocalizedMessage(UserInterfaceLanguage.Japanese),
            StringComparison.Ordinal
        );

        var コンパイル失敗 = Assert.Throws<AnalysisFailureException>(() =>
            CheckerService.Analyze(
                壊れたソース,
                cancellationToken: TestContext.Current.CancellationToken
            )
        );
        Assert.Equal(AnalysisFailureKind.Compilation, コンパイル失敗.Kind);
        Assert.Contains(
            "コンパイルエラー",
            コンパイル失敗.GetLocalizedMessage(UserInterfaceLanguage.Japanese),
            StringComparison.Ordinal
        );
    }

    private sealed class 一時失敗環境 : IDisposable
    {
        public string ルート { get; } = Path.Combine(
            Path.GetTempPath(),
            "oop-design-checker-failure-contract",
            Guid.NewGuid().ToString("N")
        );

        public 一時失敗環境()
        {
            Directory.CreateDirectory(ルート);
        }

        public string 書く(string 名前, string 内容)
        {
            var パス = Path.Combine(ルート, 名前);
            File.WriteAllText(パス, 内容);
            return パス;
        }

        public void Dispose()
        {
            if (Directory.Exists(ルート))
            {
                Directory.Delete(ルート, recursive: true);
            }
        }
    }
}
