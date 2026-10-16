using OopDesignChecker.Configuration;
using OopDesignChecker.Core;

namespace OopDesignChecker.Tests;

internal static class 設定動作テスト
{
    public static void 無効化した規則の診断を抑制する()
    {
        一時プロジェクトで実行する(rootPath =>
        {
            File.WriteAllText(
                Path.Combine(rootPath, "Sample.cs"),
                "internal sealed class Sample { public int Value; }"
            );
            File.WriteAllText(
                Path.Combine(rootPath, "oop-design-checker.json"),
                """
                {
                  "disabledRules": ["OOP106"]
                }
                """
            );

            var result = CheckerService.Analyze(rootPath);
            if (result.Diagnostics.Any(diagnostic => diagnostic.Rule.Id == "OOP106"))
            {
                throw new InvalidOperationException(
                    "OOP106 should be suppressed by disabledRules."
                );
            }
        });
    }

    public static void 無視対象のパスを除外する()
    {
        一時プロジェクトで実行する(rootPath =>
        {
            File.WriteAllText(
                Path.Combine(rootPath, "Clean.cs"),
                "internal sealed class Clean { private int _value; public int Value => _value; }"
            );

            var generatedPath = Path.Combine(rootPath, "Generated");
            Directory.CreateDirectory(generatedPath);
            File.WriteAllText(
                Path.Combine(generatedPath, "Bad.cs"),
                "internal sealed class GeneratedSample { public int Value; }"
            );

            File.WriteAllText(
                Path.Combine(rootPath, "oop-design-checker.json"),
                """
                {
                  "ignoredPaths": ["**/Generated/**"]
                }
                """
            );

            var result = CheckerService.Analyze(rootPath);
            if (result.Diagnostics.Any(diagnostic => diagnostic.Rule.Id == "OOP106"))
            {
                throw new InvalidOperationException(
                    "Files matched by ignoredPaths must not be analyzed."
                );
            }
        });
    }

    public static void 失敗基準を読み込む()
    {
        一時プロジェクトで実行する(rootPath =>
        {
            File.WriteAllText(
                Path.Combine(rootPath, "Clean.cs"),
                "internal sealed class Clean { private int _value; public int Value => _value; }"
            );
            File.WriteAllText(
                Path.Combine(rootPath, "oop-design-checker.json"),
                """
                {
                  "failureThreshold": "attention"
                }
                """
            );

            var result = CheckerService.Analyze(rootPath);
            if (result.FailureThreshold != DesignDiagnosticSeverity.Attention)
            {
                throw new InvalidOperationException(
                    $"Expected Attention threshold, found {result.FailureThreshold}."
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
        一時プロジェクトで実行する(rootPath =>
        {
            var nestedPath = Path.Combine(rootPath, "src", "App");
            Directory.CreateDirectory(nestedPath);
            var sourcePath = Path.Combine(nestedPath, "Sample.cs");
            File.WriteAllText(
                sourcePath,
                "internal sealed class Sample { private int _value; public int Value => _value; }"
            );

            var configurationPath = Path.Combine(rootPath, "oop-design-checker.json");
            File.WriteAllText(
                configurationPath,
                """
                {
                  "failureThreshold": "attention"
                }
                """
            );

            var result = CheckerService.Analyze(sourcePath);
            設定パスを確認する(result.ConfigurationPath, configurationPath);
            if (result.FailureThreshold != DesignDiagnosticSeverity.Attention)
            {
                throw new InvalidOperationException(
                    "Nested target did not inherit the parent checker configuration."
                );
            }
        });
    }

    private static void 最も近い設定を優先する()
    {
        一時プロジェクトで実行する(rootPath =>
        {
            File.WriteAllText(
                Path.Combine(rootPath, "oop-design-checker.json"),
                """
                {
                  "failureThreshold": "warning"
                }
                """
            );

            var nestedPath = Path.Combine(rootPath, "src", "App");
            Directory.CreateDirectory(nestedPath);
            var nestedConfigurationPath = Path.Combine(nestedPath, "oop-design-checker.json");
            File.WriteAllText(
                nestedConfigurationPath,
                """
                {
                  "failureThreshold": "attention"
                }
                """
            );
            var sourcePath = Path.Combine(nestedPath, "Sample.cs");
            File.WriteAllText(
                sourcePath,
                "internal sealed class Sample { private int _value; public int Value => _value; }"
            );

            var result = CheckerService.Analyze(sourcePath);
            設定パスを確認する(result.ConfigurationPath, nestedConfigurationPath);
            if (result.FailureThreshold != DesignDiagnosticSeverity.Attention)
            {
                throw new InvalidOperationException(
                    "The nearest checker configuration did not win."
                );
            }
        });
    }

    private static void 明示した相対設定パスを対象ディレクトリ基準で解決する()
    {
        一時プロジェクトで実行する(rootPath =>
        {
            var targetPath = Path.Combine(rootPath, "src", "App");
            Directory.CreateDirectory(targetPath);
            var sourcePath = Path.Combine(targetPath, "Sample.cs");
            File.WriteAllText(
                sourcePath,
                "internal sealed class Sample { private int _value; public int Value => _value; }"
            );

            var configurationDirectory = Path.Combine(targetPath, "config");
            Directory.CreateDirectory(configurationDirectory);
            var configurationPath = Path.Combine(configurationDirectory, "checker.json");
            File.WriteAllText(
                configurationPath,
                """
                {
                  "failureThreshold": "attention"
                }
                """
            );

            var result = CheckerService.Analyze(sourcePath, Path.Combine("config", "checker.json"));
            設定パスを確認する(result.ConfigurationPath, configurationPath);
        });
    }

    private static void 相対設定パスを作業ディレクトリ基準で優先する()
    {
        一時プロジェクトで実行する(rootPath =>
        {
            var originalDirectory = Directory.GetCurrentDirectory();
            var currentDirectory = Path.Combine(rootPath, "cwd");
            var targetDirectory = Path.Combine(rootPath, "target");
            var relativePath = Path.Combine("config", "checker.json");
            Directory.CreateDirectory(Path.Combine(currentDirectory, "config"));
            Directory.CreateDirectory(Path.Combine(targetDirectory, "config"));

            var currentConfigurationPath = Path.Combine(currentDirectory, relativePath);
            var targetConfigurationPath = Path.Combine(targetDirectory, relativePath);
            File.WriteAllText(currentConfigurationPath, "{}");
            File.WriteAllText(targetConfigurationPath, "{}");

            try
            {
                Directory.SetCurrentDirectory(currentDirectory);
                var expectedPath = Path.GetFullPath(relativePath, Directory.GetCurrentDirectory());
                var resolved = CheckerConfigurationPathResolver.ResolveExplicit(
                    targetDirectory,
                    relativePath
                );
                設定パスを確認する(resolved, expectedPath);
            }
            finally
            {
                Directory.SetCurrentDirectory(originalDirectory);
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
        一時プロジェクトで実行する(rootPath =>
        {
            var projectPath = Path.Combine(rootPath, "FolderTarget.csproj");
            File.WriteAllText(
                projectPath,
                """
                <Project Sdk="Microsoft.NET.Sdk">
                  <PropertyGroup>
                    <TargetFramework>net10.0</TargetFramework>
                  </PropertyGroup>
                </Project>
                """
            );
            File.WriteAllText(
                Path.Combine(rootPath, "Sample.cs"),
                "internal sealed class Sample { private int _value; public int Value => _value; }"
            );

            var result = CheckerService.Analyze(rootPath);
            if (result.ConfigurationPath is not null)
            {
                throw new InvalidOperationException(
                    $"Expected built-in defaults with no configuration file, found '{result.ConfigurationPath}'."
                );
            }

            if (result.FailureThreshold != DesignDiagnosticSeverity.Danger)
            {
                throw new InvalidOperationException(
                    $"Expected default Danger threshold, found {result.FailureThreshold}."
                );
            }
        });
    }

    private static void 設定がない場合に確認先を表示する()
    {
        一時プロジェクトで実行する(rootPath =>
        {
            var targetDirectory = Path.Combine(rootPath, "target");
            Directory.CreateDirectory(targetDirectory);
            const string relativePath = "missing-checker.json";
            var currentDirectoryPath = Path.GetFullPath(
                relativePath,
                Directory.GetCurrentDirectory()
            );
            var targetRelativePath = Path.GetFullPath(relativePath, targetDirectory);

            try
            {
                _ = CheckerConfigurationPathResolver.ResolveExplicit(targetDirectory, relativePath);
            }
            catch (InvalidOperationException exception)
                when (exception.Message.Contains(currentDirectoryPath, StringComparison.Ordinal)
                    && exception.Message.Contains(targetRelativePath, StringComparison.Ordinal)
                )
            {
                return;
            }

            throw new InvalidOperationException(
                "Missing explicit configuration did not report both checked locations."
            );
        });
    }

    private static void 設定パスを確認する(string? actualPath, string expectedPath)
    {
        if (!string.Equals(actualPath, expectedPath, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"Expected configuration '{expectedPath}', found '{actualPath ?? "<none>"}'."
            );
        }
    }

    private static void 一時プロジェクトで実行する(Action<string> action)
    {
        var rootPath = Path.Combine(
            Path.GetTempPath(),
            "oop-design-checker-tests",
            Guid.NewGuid().ToString("N")
        );
        Directory.CreateDirectory(rootPath);

        try
        {
            action(rootPath);
        }
        finally
        {
            if (Directory.Exists(rootPath))
            {
                Directory.Delete(rootPath, recursive: true);
            }
        }
    }
}
