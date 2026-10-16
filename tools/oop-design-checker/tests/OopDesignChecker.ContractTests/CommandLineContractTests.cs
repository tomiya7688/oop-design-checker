using OopDesignChecker;
using OopDesignChecker.Application;
using OopDesignChecker.Core;
using OopDesignChecker.Localization;
using Xunit;

namespace OopDesignChecker.ContractTests;

public sealed class CLI契約テスト
{
    [Fact]
    public void 対象を作業ディレクトリに設定する()
    {
        var 結果 = コマンドライン引数解析器.Parse([]);

        Assert.True(結果.IsSuccess);
        Assert.NotNull(結果.Options);
        Assert.Equal(Path.GetFullPath(Directory.GetCurrentDirectory()), 結果.Options.TargetPath);
    }

    [Theory]
    [InlineData("sample.cs")]
    [InlineData("sample.csproj")]
    [InlineData("sample.sln")]
    [InlineData("sample.slnx")]
    [InlineData("sample-folder")]
    public void 対応する対象形式をアプリケーションへ渡す(string 対象)
    {
        var 結果 = コマンドライン引数解析器.Parse([対象]);

        Assert.True(結果.IsSuccess);
        Assert.NotNull(結果.Options);
        Assert.Equal(Path.GetFullPath(対象), 結果.Options.TargetPath);
    }

    [Theory]
    [InlineData("--config")]
    [InlineData("--fail-on")]
    [InlineData("--format")]
    [InlineData("--output")]
    [InlineData("--language")]
    public void 値が必要なオプションで値の欠落を拒否する(string オプション)
    {
        var 結果 = コマンドライン引数解析器.Parse([オプション]);

        Assert.False(結果.IsSuccess);
        Assert.Null(結果.Options);
        Assert.False(string.IsNullOrWhiteSpace(結果.ErrorMessage));
    }

    [Fact]
    public void 不明なオプションを拒否する()
    {
        var 結果 = コマンドライン引数解析器.Parse(["--not-a-real-option"]);

        Assert.False(結果.IsSuccess);
        Assert.Contains("--not-a-real-option", 結果.ErrorMessage, StringComparison.Ordinal);
    }

    [Fact]
    public void 複数の対象を拒否する()
    {
        var 結果 = コマンドライン引数解析器.Parse(["one.cs", "two.cs"]);

        Assert.False(結果.IsSuccess);
        Assert.Null(結果.Options);
    }

    [Theory]
    [InlineData("attention", DesignDiagnosticSeverity.Attention)]
    [InlineData("warning", DesignDiagnosticSeverity.Warning)]
    [InlineData("danger", DesignDiagnosticSeverity.Danger)]
    public void 失敗基準にすべての名前付き重要度を受け入れる(
        string 値,
        DesignDiagnosticSeverity 期待値
    )
    {
        var 結果 = コマンドライン引数解析器.Parse(["--fail-on", 値]);

        Assert.True(結果.IsSuccess);
        Assert.Equal(期待値, 結果.Options!.FailureThreshold);
    }

    [Theory]
    [InlineData("999")]
    [InlineData("critical")]
    public void 失敗基準の不正な値を拒否する(string 値)
    {
        var 結果 = コマンドライン引数解析器.Parse(["--fail-on", 値]);

        Assert.False(結果.IsSuccess);
    }

    [Theory]
    [InlineData("text", "Text")]
    [InlineData("json", "Json")]
    [InlineData("sarif", "Sarif")]
    [InlineData("github", "GitHub")]
    public void 出力形式にすべての名前付き値を受け入れる(string 値, string 期待値)
    {
        var 結果 = コマンドライン引数解析器.Parse(["--format", 値]);

        Assert.True(結果.IsSuccess);
        Assert.Equal(期待値, 結果.Options!.OutputFormat.ToString());
    }

    [Theory]
    [InlineData("999")]
    [InlineData("xml")]
    public void 出力形式の不正な値を拒否する(string 値)
    {
        var 結果 = コマンドライン引数解析器.Parse(["--format", 値]);

        Assert.False(結果.IsSuccess);
    }

    [Theory]
    [InlineData("ja", UserInterfaceLanguage.Japanese)]
    [InlineData("en", UserInterfaceLanguage.English)]
    public void 対応する言語値を受け入れる(string 値, UserInterfaceLanguage 期待値)
    {
        var 結果 = コマンドライン引数解析器.Parse(["--language", 値]);

        Assert.True(結果.IsSuccess);
        Assert.Equal(期待値, 結果.Options!.Language);
    }

    [Fact]
    public void 不明な言語値を拒否する()
    {
        var 結果 = コマンドライン引数解析器.Parse(["--language", "fr"]);

        Assert.False(結果.IsSuccess);
    }

    [Fact]
    public void 警告をエラーにする指定を警告基準へ対応させる()
    {
        var 結果 = コマンドライン引数解析器.Parse(["--warnings-as-errors"]);

        Assert.True(結果.IsSuccess);
        Assert.Equal(DesignDiagnosticSeverity.Warning, 結果.Options!.FailureThreshold);
    }

    [Fact]
    public void 詳細表示フラグを維持する()
    {
        var 結果 = コマンドライン引数解析器.Parse(["--verbose"]);

        Assert.True(結果.IsSuccess);
        Assert.True(結果.Options!.Verbose);
    }

    [Fact]
    public void 出力先パスを正規化する()
    {
        var 結果 = コマンドライン引数解析器.Parse(["--format", "json", "--output", "results.json"]);

        Assert.True(結果.IsSuccess);
        Assert.Equal(Path.GetFullPath("results.json"), 結果.Options!.OutputPath);
    }

    [Fact]
    public void GitHub注釈形式で出力ファイルを拒否する()
    {
        var 結果 = コマンドライン引数解析器.Parse([
            "--format",
            "github",
            "--output",
            "annotations.txt",
        ]);

        Assert.False(結果.IsSuccess);
    }

    [Fact]
    public void バージョン別名で製品バージョンを返す()
    {
        バージョンを確認する("--version");
        バージョンを確認する("-V");
    }

    [Fact]
    public void ヘルプ表示で制御された終了結果を返す()
    {
        var 結果 = CLIを実行する(["--help"]);

        Assert.Equal(2, 結果.終了コード);
        Assert.Contains(
            コマンドライン引数解析器.Usage(UserInterfaceLanguage.Japanese),
            結果.エラー,
            StringComparison.Ordinal
        );
    }

    [Fact]
    public void CLI終了コードで成功と診断失敗と使用方法エラーを区別する()
    {
        一時ディレクトリで実行する(ルート =>
        {
            var 正常 = Path.Combine(ルート, "Clean.cs");
            var 危険診断 = Path.Combine(ルート, "Danger.cs");
            File.WriteAllText(
                正常,
                "internal sealed class Clean { private int _value; public int Value => _value; }"
            );
            File.WriteAllText(危険診断, "internal sealed class Danger { public int Value; }");

            Assert.Equal(0, CLIを実行する([正常, "--fail-on", "danger"]).終了コード);
            Assert.Equal(1, CLIを実行する([危険診断, "--fail-on", "danger"]).終了コード);
            Assert.Equal(2, CLIを実行する([Path.Combine(ルート, "missing-target")]).終了コード);
        });
    }

    [Fact]
    public void JSON出力で指定ファイルを作成する()
    {
        一時ディレクトリで実行する(ルート =>
        {
            var ソース = Path.Combine(ルート, "Danger.cs");
            var 出力 = Path.Combine(ルート, "diagnostics.json");
            File.WriteAllText(ソース, "internal sealed class Danger { public int Value; }");

            var 結果 = CLIを実行する([
                ソース,
                "--format",
                "json",
                "--output",
                出力,
                "--fail-on",
                "danger",
            ]);

            Assert.Equal(1, 結果.終了コード);
            Assert.True(File.Exists(出力));
            Assert.Contains(
                @"""ruleId"": ""OOP106""",
                File.ReadAllText(出力),
                StringComparison.Ordinal
            );
        });
    }

    [Fact]
    public void 出力書き込み失敗で制御された終了コードを返す()
    {
        一時ディレクトリで実行する(ルート =>
        {
            var ソース = Path.Combine(ルート, "Clean.cs");
            var 出力 = Path.Combine(ルート, "output-target");
            Directory.CreateDirectory(出力);
            File.WriteAllText(
                ソース,
                "internal sealed class Clean { private int _value; public int Value => _value; }"
            );

            var 結果 = CLIを実行する([ソース, "--format", "json", "--output", 出力]);

            Assert.Equal(2, 結果.終了コード);
            Assert.False(string.IsNullOrWhiteSpace(結果.エラー));
        });
    }

    private static void バージョンを確認する(string オプション)
    {
        var 結果 = CLIを実行する([オプション]);

        Assert.Equal(0, 結果.終了コード);
        Assert.Equal("1.0.0", 結果.出力.Trim());
        Assert.True(string.IsNullOrWhiteSpace(結果.エラー));
    }

    private static コマンド実行結果 CLIを実行する(IReadOnlyList<string> 引数)
    {
        var 元の標準出力 = Console.Out;
        var 元のエラー = Console.Error;
        using var 出力 = new StringWriter();
        using var エラー = new StringWriter();
        try
        {
            Console.SetOut(出力);
            Console.SetError(エラー);
            var 終了コード = CheckerCommandLine.Run(引数);
            return new コマンド実行結果(終了コード, 出力.ToString(), エラー.ToString());
        }
        finally
        {
            Console.SetOut(元の標準出力);
            Console.SetError(元のエラー);
        }
    }

    private static void 一時ディレクトリで実行する(Action<string> 処理)
    {
        var ルート = Path.Combine(
            Path.GetTempPath(),
            "oop-design-checker-contract-tests",
            Guid.NewGuid().ToString("N")
        );
        Directory.CreateDirectory(ルート);
        try
        {
            処理(ルート);
        }
        finally
        {
            Directory.Delete(ルート, recursive: true);
        }
    }

    private sealed record コマンド実行結果(int 終了コード, string 出力, string エラー);
}
