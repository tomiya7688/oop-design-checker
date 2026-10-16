using System.Text.Json;
using OopDesignChecker.Application;
using OopDesignChecker.Core;
using OopDesignChecker.Output;

namespace OopDesignChecker.Tests;

internal static class 出力形式スモークテスト
{
    public static void 実行する()
    {
        CLIで出力形式と出力先を解析する();
        相対設定パスを維持する();
        CLIで製品バージョンを表示する();
        GitHub形式で出力ファイルを拒否する();
        数値の失敗基準を拒否する();
        数値の出力形式を拒否する();
        JSON出力を機械可読にする();
        SARIF出力を機械可読にする();
        SARIF出力で不明なパスを扱う();
        GitHub注釈で特殊文字をエスケープする();
    }

    private static void CLIで出力形式と出力先を解析する()
    {
        var 結果 = コマンドライン引数解析器.Parse([
            "sample.cs",
            "--format",
            "sarif",
            "--output",
            "results.sarif",
        ]);

        if (
            !結果.IsSuccess
            || 結果.Options is null
            || 結果.Options.OutputFormat != 診断出力形式.Sarif
            || !結果.Options.OutputPath!.EndsWith("results.sarif", StringComparison.Ordinal)
        )
        {
            throw new InvalidOperationException("Expected SARIF output CLI options to parse.");
        }
    }

    private static void 相対設定パスを維持する()
    {
        var 相対パス = Path.Combine("config", "checker.json");
        var 結果 = コマンドライン引数解析器.Parse(["sample.cs", "--config", 相対パス]);

        if (!結果.IsSuccess || 結果.Options is null || 結果.Options.ConfigurationPath != 相対パス)
        {
            throw new InvalidOperationException(
                "Relative --config paths must reach the configuration loader without being converted to a CWD-based absolute path."
            );
        }
    }

    private static void CLIで製品バージョンを表示する()
    {
        var 元の標準出力 = Console.Out;
        using var 捕捉内容 = new StringWriter();
        try
        {
            Console.SetOut(捕捉内容);
            var 終了コード = CheckerCommandLine.Run(["--version"]);
            if (終了コード != 0)
            {
                throw new InvalidOperationException(
                    $"Expected --version to succeed, exit code was {終了コード}."
                );
            }
        }
        finally
        {
            Console.SetOut(元の標準出力);
        }

        if (!string.Equals(捕捉内容.ToString().Trim(), "1.0.0", StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"Expected product version 1.0.0, found '{捕捉内容.ToString().Trim()}'."
            );
        }
    }

    private static void GitHub形式で出力ファイルを拒否する()
    {
        var 結果 = コマンドライン引数解析器.Parse([
            "sample.cs",
            "--format",
            "github",
            "--output",
            "annotations.txt",
        ]);

        if (結果.IsSuccess)
        {
            throw new InvalidOperationException(
                "GitHub annotation output should reject --output because annotations must use stdout."
            );
        }
    }

    private static void 数値の失敗基準を拒否する()
    {
        var 結果 = コマンドライン引数解析器.Parse(["sample.cs", "--fail-on", "999"]);
        if (結果.IsSuccess)
        {
            throw new InvalidOperationException("Numeric --fail-on values must be rejected.");
        }
    }

    private static void 数値の出力形式を拒否する()
    {
        var 結果 = コマンドライン引数解析器.Parse(["sample.cs", "--format", "999"]);
        if (結果.IsSuccess)
        {
            throw new InvalidOperationException("Numeric --format values must be rejected.");
        }
    }

    private static void JSON出力を機械可読にする()
    {
        var パス = 一時パスを作る("json");
        try
        {
            new JSON診断書き込み器(パス).Write([診断を作る()], verbose: true);
            using var 文書 = JsonDocument.Parse(File.ReadAllText(パス));
            var ルート = 文書.RootElement;

            if (
                ルート.GetProperty("version").GetInt32() != 1
                || ルート.GetProperty("diagnostics")[0].GetProperty("ruleId").GetString()
                    != "OOP999"
                || ルート.GetProperty("summary").GetProperty("warning").GetInt32() != 1
            )
            {
                throw new InvalidOperationException(
                    "JSON diagnostic document had unexpected content."
                );
            }
        }
        finally
        {
            存在する場合は削除する(パス);
        }
    }

    private static void SARIF出力を機械可読にする()
    {
        var パス = 一時パスを作る("sarif");
        try
        {
            new SARIF診断書き込み器(パス).Write([診断を作る()], verbose: false);
            using var 文書 = JsonDocument.Parse(File.ReadAllText(パス));
            var ルート = 文書.RootElement;
            var 結果 = ルート.GetProperty("runs")[0].GetProperty("results")[0];

            if (
                ルート.GetProperty("version").GetString() != "2.1.0"
                || 結果.GetProperty("ruleId").GetString() != "OOP999"
                || 結果.GetProperty("level").GetString() != "warning"
            )
            {
                throw new InvalidOperationException("SARIF document had unexpected content.");
            }
        }
        finally
        {
            存在する場合は削除する(パス);
        }
    }

    private static void SARIF出力で不明なパスを扱う()
    {
        var パス = 一時パスを作る("sarif");
        try
        {
            new SARIF診断書き込み器(パス).Write(
                [診断を作る(ファイルパス: "<unknown>")],
                verbose: false
            );

            using var 文書 = JsonDocument.Parse(File.ReadAllText(パス));
            var 結果 = 文書.RootElement.GetProperty("runs")[0].GetProperty("results")[0];

            if (結果.GetProperty("ruleId").GetString() != "OOP999")
            {
                throw new InvalidOperationException(
                    "SARIF output lost the diagnostic with an unknown source path."
                );
            }

            if (結果.GetProperty("locations").GetArrayLength() != 0)
            {
                throw new InvalidOperationException(
                    "SARIF output must not invent a file URI for an unknown source path."
                );
            }
        }
        finally
        {
            存在する場合は削除する(パス);
        }
    }

    private static void GitHub注釈で特殊文字をエスケープする()
    {
        var 元の標準出力 = Console.Out;
        using var 捕捉内容 = new StringWriter();
        try
        {
            Console.SetOut(捕捉内容);
            var 診断 = 診断を作る(
                ファイルパス: Path.Combine("sample,dir", "file:part.cs"),
                メッセージ: "first%line\nsecond"
            );
            new GitHub注釈診断書き込み器().Write([診断], verbose: false);
        }
        finally
        {
            Console.SetOut(元の標準出力);
        }

        var 出力 = 捕捉内容.ToString();
        if (
            !出力.Contains("::warning ", StringComparison.Ordinal)
            || !出力.Contains("%2C", StringComparison.Ordinal)
            || !出力.Contains("%3A", StringComparison.Ordinal)
            || !出力.Contains("%25", StringComparison.Ordinal)
            || !出力.Contains("%0A", StringComparison.Ordinal)
        )
        {
            throw new InvalidOperationException(
                $"GitHub annotation escaping was incomplete: {出力}"
            );
        }
    }

    private static DesignDiagnostic 診断を作る(
        string? ファイルパス = null,
        string メッセージ = "Sample diagnostic"
    ) =>
        new(
            new RuleDescriptor("OOP999", "Sample rule", DesignDiagnosticSeverity.Warning),
            DesignDiagnosticSeverity.Warning,
            メッセージ,
            "Sample.Member",
            new SourceLocation(ファイルパス ?? Path.GetFullPath("sample.cs"), 3, 7)
        );

    private static string 一時パスを作る(string 拡張子) =>
        Path.Combine(Path.GetTempPath(), $"oop-checker-{Guid.NewGuid():N}.{拡張子}");

    private static void 存在する場合は削除する(string パス)
    {
        if (File.Exists(パス))
        {
            File.Delete(パス);
        }
    }
}
