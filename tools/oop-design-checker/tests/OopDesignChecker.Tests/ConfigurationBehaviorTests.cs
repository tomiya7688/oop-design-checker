using OopDesignChecker.Configuration;
using OopDesignChecker.Core;

namespace OopDesignChecker.Tests;

internal static class 設定動作テスト
{
    public static void 無効化した規則の診断を抑制する()
    {
        一時プロジェクトで実行する(ルートパス =>
        {
            File.WriteAllText(
                Path.Combine(ルートパス, "Sample.cs"),
                "internal sealed class Sample { public int Value; }"
            );
            File.WriteAllText(
                Path.Combine(ルートパス, "oop-design-checker.json"),
                """
                {
                  "disabledRules": ["OOP106"]
                }
                """
            );

            var 結果 = CheckerService.Analyze(ルートパス);
            if (結果.Diagnostics.Any(診断 => 診断.Rule.Id == "OOP106"))
            {
                throw new InvalidOperationException(
                    "OOP106 should be suppressed by disabledRules."
                );
            }
        });
    }

    public static void 無視対象のパスを除外する()
    {
        一時プロジェクトで実行する(ルートパス =>
        {
            File.WriteAllText(
                Path.Combine(ルートパス, "Clean.cs"),
                "internal sealed class Clean { private int _value; public int Value => _value; }"
            );

            var 生成したパス = Path.Combine(ルートパス, "Generated");
            Directory.CreateDirectory(生成したパス);
            File.WriteAllText(
                Path.Combine(生成したパス, "Bad.cs"),
                "internal sealed class GeneratedSample { public int Value; }"
            );

            File.WriteAllText(
                Path.Combine(ルートパス, "oop-design-checker.json"),
                """
                {
                  "ignoredPaths": ["**/Generated/**"]
                }
                """
            );

            var 結果 = CheckerService.Analyze(ルートパス);
            if (結果.Diagnostics.Any(診断 => 診断.Rule.Id == "OOP106"))
            {
                throw new InvalidOperationException(
                    "Files matched by ignoredPaths must not be analyzed."
                );
            }
        });
    }

    public static void 失敗基準を読み込む()
    {
        一時プロジェクトで実行する(ルートパス =>
        {
            File.WriteAllText(
                Path.Combine(ルートパス, "Clean.cs"),
                "internal sealed class Clean { private int _value; public int Value => _value; }"
            );
            File.WriteAllText(
                Path.Combine(ルートパス, "oop-design-checker.json"),
                """
                {
                  "failureThreshold": "attention"
                }
                """
            );

            var 結果 = CheckerService.Analyze(ルートパス);
            if (結果.FailureThreshold != DesignDiagnosticSeverity.Attention)
            {
                throw new InvalidOperationException(
                    $"Expected Attention threshold, found {結果.FailureThreshold}."
                );
            }
        });

        入れ子対象に親設定を適用する();
        最も近い設定を優先する();
        明示した相対設定パスを対象ディレクトリ基準で解決する();
        相対設定パスを作業ディレクトリ基準で優先する();
        設定がない場合に確認先を表示する();
        設定がない場合に既定値を使う();
        数値の失敗基準を拒否する();
    }

    private static void 入れ子対象に親設定を適用する()
    {
        一時プロジェクトで実行する(ルートパス =>
        {
            var 入れ子パス = Path.Combine(ルートパス, "src", "App");
            Directory.CreateDirectory(入れ子パス);
            var ソースパス = Path.Combine(入れ子パス, "Sample.cs");
            File.WriteAllText(
                ソースパス,
                "internal sealed class Sample { private int _value; public int Value => _value; }"
            );

            var 設定パス = Path.Combine(ルートパス, "oop-design-checker.json");
            File.WriteAllText(
                設定パス,
                """
                {
                  "failureThreshold": "attention"
                }
                """
            );

            var 結果 = CheckerService.Analyze(ソースパス);
            設定パスを確認する(結果.ConfigurationPath, 設定パス);
            if (結果.FailureThreshold != DesignDiagnosticSeverity.Attention)
            {
                throw new InvalidOperationException(
                    "Nested target did not inherit the parent checker configuration."
                );
            }
        });
    }

    private static void 最も近い設定を優先する()
    {
        一時プロジェクトで実行する(ルートパス =>
        {
            File.WriteAllText(
                Path.Combine(ルートパス, "oop-design-checker.json"),
                """
                {
                  "failureThreshold": "warning"
                }
                """
            );

            var 入れ子パス = Path.Combine(ルートパス, "src", "App");
            Directory.CreateDirectory(入れ子パス);
            var 入れ子設定パス = Path.Combine(入れ子パス, "oop-design-checker.json");
            File.WriteAllText(
                入れ子設定パス,
                """
                {
                  "failureThreshold": "attention"
                }
                """
            );
            var ソースパス = Path.Combine(入れ子パス, "Sample.cs");
            File.WriteAllText(
                ソースパス,
                "internal sealed class Sample { private int _value; public int Value => _value; }"
            );

            var 結果 = CheckerService.Analyze(ソースパス);
            設定パスを確認する(結果.ConfigurationPath, 入れ子設定パス);
            if (結果.FailureThreshold != DesignDiagnosticSeverity.Attention)
            {
                throw new InvalidOperationException(
                    "The nearest checker configuration did not win."
                );
            }
        });
    }

    private static void 明示した相対設定パスを対象ディレクトリ基準で解決する()
    {
        一時プロジェクトで実行する(ルートパス =>
        {
            var 対象パス = Path.Combine(ルートパス, "src", "App");
            Directory.CreateDirectory(対象パス);
            var ソースパス = Path.Combine(対象パス, "Sample.cs");
            File.WriteAllText(
                ソースパス,
                "internal sealed class Sample { private int _value; public int Value => _value; }"
            );

            var 設定ディレクトリ = Path.Combine(対象パス, "config");
            Directory.CreateDirectory(設定ディレクトリ);
            var 設定パス = Path.Combine(設定ディレクトリ, "checker.json");
            File.WriteAllText(
                設定パス,
                """
                {
                  "failureThreshold": "attention"
                }
                """
            );

            var 結果 = CheckerService.Analyze(ソースパス, Path.Combine("config", "checker.json"));
            設定パスを確認する(結果.ConfigurationPath, 設定パス);
        });
    }

    private static void 相対設定パスを作業ディレクトリ基準で優先する()
    {
        一時プロジェクトで実行する(ルートパス =>
        {
            var 元のディレクトリ = Directory.GetCurrentDirectory();
            var 現在のディレクトリ = Path.Combine(ルートパス, "cwd");
            var 対象ディレクトリ = Path.Combine(ルートパス, "target");
            var 相対パス = Path.Combine("config", "checker.json");
            Directory.CreateDirectory(Path.Combine(現在のディレクトリ, "config"));
            Directory.CreateDirectory(Path.Combine(対象ディレクトリ, "config"));

            var 現在の設定パス = Path.Combine(現在のディレクトリ, 相対パス);
            var 対象設定パス = Path.Combine(対象ディレクトリ, 相対パス);
            File.WriteAllText(現在の設定パス, "{}");
            File.WriteAllText(対象設定パス, "{}");

            try
            {
                Directory.SetCurrentDirectory(現在のディレクトリ);
                var 期待するパス = Path.GetFullPath(相対パス, Directory.GetCurrentDirectory());
                var 解決結果 = CheckerConfigurationPathResolver.ResolveExplicit(
                    対象ディレクトリ,
                    相対パス
                );
                設定パスを確認する(解決結果, 期待するパス);
            }
            finally
            {
                Directory.SetCurrentDirectory(元のディレクトリ);
            }
        });
    }

    private static void 数値の失敗基準を拒否する()
    {
        try
        {
            _ = CheckerConfigurationJson.Parse(
                """
                {
                  "failureThreshold": 999
                }
                """
            );
        }
        catch (InvalidOperationException)
        {
            return;
        }

        throw new InvalidOperationException("Numeric failureThreshold values must be rejected.");
    }

    private static void 設定がない場合に既定値を使う()
    {
        一時プロジェクトで実行する(ルートパス =>
        {
            var プロジェクトパス = Path.Combine(ルートパス, "FolderTarget.csproj");
            File.WriteAllText(
                プロジェクトパス,
                """
                <Project Sdk="Microsoft.NET.Sdk">
                  <PropertyGroup>
                    <TargetFramework>net10.0</TargetFramework>
                  </PropertyGroup>
                </Project>
                """
            );
            File.WriteAllText(
                Path.Combine(ルートパス, "Sample.cs"),
                "internal sealed class Sample { private int _value; public int Value => _value; }"
            );

            foreach (var 設定パス in new string?[] { null, string.Empty, "  \t  " })
            {
                var 結果 = CheckerService.Analyze(ルートパス, 設定パス);
                if (結果.ConfigurationPath is not null)
                {
                    throw new InvalidOperationException(
                        $"Expected built-in defaults with no configuration file, found '{結果.ConfigurationPath}'."
                    );
                }

                if (
                    結果.FailureThreshold != DesignDiagnosticSeverity.Danger
                    || 結果.Configuration.FailureThreshold != DesignDiagnosticSeverity.Danger
                    || 結果.Configuration.IgnoredPaths.Count != 0
                    || 結果.Configuration.DisabledRules.Count != 0
                    || 結果.Configuration.RuleSettings.Oop304.WarningDepth != 4
                )
                {
                    throw new InvalidOperationException(
                        "No explicit configuration must use the complete built-in defaults."
                    );
                }
            }
        });
    }

    private static void 設定がない場合に確認先を表示する()
    {
        一時プロジェクトで実行する(ルートパス =>
        {
            var 対象ディレクトリ = Path.Combine(ルートパス, "target");
            Directory.CreateDirectory(対象ディレクトリ);
            const string 相対パス = "missing-checker.json";
            var 現在のディレクトリパス = Path.GetFullPath(
                相対パス,
                Directory.GetCurrentDirectory()
            );
            var 対象相対パス = Path.GetFullPath(相対パス, 対象ディレクトリ);

            try
            {
                _ = CheckerConfigurationPathResolver.ResolveExplicit(対象ディレクトリ, 相対パス);
            }
            catch (InvalidOperationException 例外)
                when (例外.Message.Contains(現在のディレクトリパス, StringComparison.Ordinal)
                    && 例外.Message.Contains(対象相対パス, StringComparison.Ordinal)
                )
            {
                return;
            }

            throw new InvalidOperationException(
                "Missing explicit configuration did not report both checked locations."
            );
        });
    }

    private static void 設定パスを確認する(string? 実際のパス, string 期待するパス)
    {
        if (!string.Equals(実際のパス, 期待するパス, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"Expected configuration '{期待するパス}', found '{実際のパス ?? "<none>"}'."
            );
        }
    }

    private static void 一時プロジェクトで実行する(Action<string> 処理)
    {
        var ルートパス = Path.Combine(
            Path.GetTempPath(),
            "oop-design-checker-tests",
            Guid.NewGuid().ToString("N")
        );
        Directory.CreateDirectory(ルートパス);

        try
        {
            処理(ルートパス);
        }
        finally
        {
            if (Directory.Exists(ルートパス))
            {
                Directory.Delete(ルートパス, recursive: true);
            }
        }
    }
}
