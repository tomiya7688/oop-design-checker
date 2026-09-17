using OopDesignChecker.Configuration;
using OopDesignChecker.Core;

namespace OopDesignChecker.Tests;

internal static class ConfigurationBehaviorTests
{
    public static void DisabledRulesAreSuppressed()
    {
        WithTemporaryProject(rootPath =>
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

    public static void IgnoredPathsAreExcluded()
    {
        WithTemporaryProject(rootPath =>
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

    public static void FailureThresholdIsLoaded()
    {
        WithTemporaryProject(rootPath =>
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

        ParentConfigurationIsDiscoveredForNestedTarget();
        NearestConfigurationWins();
        ExplicitRelativeConfigurationCanUseTargetDirectory();
        ExplicitResolverPrefersCurrentDirectory();
        MissingExplicitConfigurationListsCheckedLocations();
    }

    private static void ParentConfigurationIsDiscoveredForNestedTarget()
    {
        WithTemporaryProject(rootPath =>
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
            AssertConfigurationPath(result.ConfigurationPath, configurationPath);
            if (result.FailureThreshold != DesignDiagnosticSeverity.Attention)
            {
                throw new InvalidOperationException(
                    "Nested target did not inherit the parent checker configuration."
                );
            }
        });
    }

    private static void NearestConfigurationWins()
    {
        WithTemporaryProject(rootPath =>
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
            AssertConfigurationPath(result.ConfigurationPath, nestedConfigurationPath);
            if (result.FailureThreshold != DesignDiagnosticSeverity.Attention)
            {
                throw new InvalidOperationException(
                    "The nearest checker configuration did not win."
                );
            }
        });
    }

    private static void ExplicitRelativeConfigurationCanUseTargetDirectory()
    {
        WithTemporaryProject(rootPath =>
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
            AssertConfigurationPath(result.ConfigurationPath, configurationPath);
        });
    }

    private static void ExplicitResolverPrefersCurrentDirectory()
    {
        WithTemporaryProject(rootPath =>
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
                var resolved = CheckerConfigurationPathResolver.ResolveExplicit(
                    targetDirectory,
                    relativePath
                );
                AssertConfigurationPath(resolved, currentConfigurationPath);
            }
            finally
            {
                Directory.SetCurrentDirectory(originalDirectory);
            }
        });
    }

    private static void MissingExplicitConfigurationListsCheckedLocations()
    {
        WithTemporaryProject(rootPath =>
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
                    && exception.Message.Contains(targetRelativePath, StringComparison.Ordinal))
            {
                return;
            }

            throw new InvalidOperationException(
                "Missing explicit configuration did not report both checked locations."
            );
        });
    }

    private static void AssertConfigurationPath(string? actualPath, string expectedPath)
    {
        if (!string.Equals(actualPath, expectedPath, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"Expected configuration '{expectedPath}', found '{actualPath ?? "<none>"}'."
            );
        }
    }

    private static void WithTemporaryProject(Action<string> action)
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
