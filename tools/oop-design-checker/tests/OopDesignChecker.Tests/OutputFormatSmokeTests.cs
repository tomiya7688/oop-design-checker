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
        var result = コマンドライン引数解析器.Parse([
            "sample.cs",
            "--format",
            "sarif",
            "--output",
            "results.sarif",
        ]);

        if (
            !result.IsSuccess
            || result.Options is null
            || result.Options.OutputFormat != 診断出力形式.Sarif
            || !result.Options.OutputPath!.EndsWith("results.sarif", StringComparison.Ordinal)
        )
        {
            throw new InvalidOperationException("Expected SARIF output CLI options to parse.");
        }
    }

    private static void 相対設定パスを維持する()
    {
        var relativePath = Path.Combine("config", "checker.json");
        var result = コマンドライン引数解析器.Parse(["sample.cs", "--config", relativePath]);

        if (
            !result.IsSuccess
            || result.Options is null
            || result.Options.ConfigurationPath != relativePath
        )
        {
            throw new InvalidOperationException(
                "Relative --config paths must reach the configuration loader without being converted to a CWD-based absolute path."
            );
        }
    }

    private static void CLIで製品バージョンを表示する()
    {
        var originalOut = Console.Out;
        using var captured = new StringWriter();
        try
        {
            Console.SetOut(captured);
            var exitCode = CheckerCommandLine.Run(["--version"]);
            if (exitCode != 0)
            {
                throw new InvalidOperationException(
                    $"Expected --version to succeed, exit code was {exitCode}."
                );
            }
        }
        finally
        {
            Console.SetOut(originalOut);
        }

        if (!string.Equals(captured.ToString().Trim(), "1.0.0", StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"Expected product version 1.0.0, found '{captured.ToString().Trim()}'."
            );
        }
    }

    private static void GitHub形式で出力ファイルを拒否する()
    {
        var result = コマンドライン引数解析器.Parse([
            "sample.cs",
            "--format",
            "github",
            "--output",
            "annotations.txt",
        ]);

        if (result.IsSuccess)
        {
            throw new InvalidOperationException(
                "GitHub annotation output should reject --output because annotations must use stdout."
            );
        }
    }

    private static void 数値の失敗基準を拒否する()
    {
        var result = コマンドライン引数解析器.Parse(["sample.cs", "--fail-on", "999"]);
        if (result.IsSuccess)
        {
            throw new InvalidOperationException("Numeric --fail-on values must be rejected.");
        }
    }

    private static void 数値の出力形式を拒否する()
    {
        var result = コマンドライン引数解析器.Parse(["sample.cs", "--format", "999"]);
        if (result.IsSuccess)
        {
            throw new InvalidOperationException("Numeric --format values must be rejected.");
        }
    }

    private static void JSON出力を機械可読にする()
    {
        var path = 一時パスを作る("json");
        try
        {
            new JSON診断書き込み器(path).Write([診断を作る()], verbose: true);
            using var document = JsonDocument.Parse(File.ReadAllText(path));
            var root = document.RootElement;

            if (
                root.GetProperty("version").GetInt32() != 1
                || root.GetProperty("diagnostics")[0].GetProperty("ruleId").GetString() != "OOP999"
                || root.GetProperty("summary").GetProperty("warning").GetInt32() != 1
            )
            {
                throw new InvalidOperationException(
                    "JSON diagnostic document had unexpected content."
                );
            }
        }
        finally
        {
            存在する場合は削除する(path);
        }
    }

    private static void SARIF出力を機械可読にする()
    {
        var path = 一時パスを作る("sarif");
        try
        {
            new SARIF診断書き込み器(path).Write([診断を作る()], verbose: false);
            using var document = JsonDocument.Parse(File.ReadAllText(path));
            var root = document.RootElement;
            var result = root.GetProperty("runs")[0].GetProperty("results")[0];

            if (
                root.GetProperty("version").GetString() != "2.1.0"
                || result.GetProperty("ruleId").GetString() != "OOP999"
                || result.GetProperty("level").GetString() != "warning"
            )
            {
                throw new InvalidOperationException("SARIF document had unexpected content.");
            }
        }
        finally
        {
            存在する場合は削除する(path);
        }
    }

    private static void SARIF出力で不明なパスを扱う()
    {
        var path = 一時パスを作る("sarif");
        try
        {
            new SARIF診断書き込み器(path).Write(
                [診断を作る(filePath: "<unknown>")],
                verbose: false
            );

            using var document = JsonDocument.Parse(File.ReadAllText(path));
            var result = document.RootElement.GetProperty("runs")[0].GetProperty("results")[0];

            if (result.GetProperty("ruleId").GetString() != "OOP999")
            {
                throw new InvalidOperationException(
                    "SARIF output lost the diagnostic with an unknown source path."
                );
            }

            if (result.GetProperty("locations").GetArrayLength() != 0)
            {
                throw new InvalidOperationException(
                    "SARIF output must not invent a file URI for an unknown source path."
                );
            }
        }
        finally
        {
            存在する場合は削除する(path);
        }
    }

    private static void GitHub注釈で特殊文字をエスケープする()
    {
        var originalOut = Console.Out;
        using var captured = new StringWriter();
        try
        {
            Console.SetOut(captured);
            var diagnostic = 診断を作る(
                filePath: Path.Combine("sample,dir", "file:part.cs"),
                message: "first%line\nsecond"
            );
            new GitHub注釈診断書き込み器().Write([diagnostic], verbose: false);
        }
        finally
        {
            Console.SetOut(originalOut);
        }

        var output = captured.ToString();
        if (
            !output.Contains("::warning ", StringComparison.Ordinal)
            || !output.Contains("%2C", StringComparison.Ordinal)
            || !output.Contains("%3A", StringComparison.Ordinal)
            || !output.Contains("%25", StringComparison.Ordinal)
            || !output.Contains("%0A", StringComparison.Ordinal)
        )
        {
            throw new InvalidOperationException(
                $"GitHub annotation escaping was incomplete: {output}"
            );
        }
    }

    private static DesignDiagnostic 診断を作る(
        string? filePath = null,
        string message = "Sample diagnostic"
    ) =>
        new(
            new RuleDescriptor("OOP999", "Sample rule", DesignDiagnosticSeverity.Warning),
            DesignDiagnosticSeverity.Warning,
            message,
            "Sample.Member",
            new SourceLocation(filePath ?? Path.GetFullPath("sample.cs"), 3, 7)
        );

    private static string 一時パスを作る(string extension) =>
        Path.Combine(Path.GetTempPath(), $"oop-checker-{Guid.NewGuid():N}.{extension}");

    private static void 存在する場合は削除する(string path)
    {
        if (File.Exists(path))
        {
            File.Delete(path);
        }
    }
}
