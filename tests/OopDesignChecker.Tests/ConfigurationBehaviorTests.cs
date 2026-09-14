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
                "internal sealed class Sample { public int Value; }");
            File.WriteAllText(
                Path.Combine(rootPath, "oop-design-checker.json"),
                """
                {
                  "disabledRules": ["OOP106"]
                }
                """);

            var result = CheckerService.Analyze(rootPath);
            if (result.Diagnostics.Any(diagnostic => diagnostic.Rule.Id == "OOP106"))
            {
                throw new InvalidOperationException("OOP106 should be suppressed by disabledRules.");
            }
        });
    }

    public static void IgnoredPathsAreExcluded()
    {
        WithTemporaryProject(rootPath =>
        {
            File.WriteAllText(
                Path.Combine(rootPath, "Clean.cs"),
                "internal sealed class Clean { private int _value; public int Value => _value; }");

            var generatedPath = Path.Combine(rootPath, "Generated");
            Directory.CreateDirectory(generatedPath);
            File.WriteAllText(
                Path.Combine(generatedPath, "Bad.cs"),
                "internal sealed class GeneratedSample { public int Value; }");

            File.WriteAllText(
                Path.Combine(rootPath, "oop-design-checker.json"),
                """
                {
                  "ignoredPaths": ["**/Generated/**"]
                }
                """);

            var result = CheckerService.Analyze(rootPath);
            if (result.Diagnostics.Any(diagnostic => diagnostic.Rule.Id == "OOP106"))
            {
                throw new InvalidOperationException("Files matched by ignoredPaths must not be analyzed.");
            }
        });
    }

    public static void FailureThresholdIsLoaded()
    {
        WithTemporaryProject(rootPath =>
        {
            File.WriteAllText(
                Path.Combine(rootPath, "Clean.cs"),
                "internal sealed class Clean { private int _value; public int Value => _value; }");
            File.WriteAllText(
                Path.Combine(rootPath, "oop-design-checker.json"),
                """
                {
                  "failureThreshold": "attention"
                }
                """);

            var result = CheckerService.Analyze(rootPath);
            if (result.FailureThreshold != DesignDiagnosticSeverity.Attention)
            {
                throw new InvalidOperationException(
                    $"Expected Attention threshold, found {result.FailureThreshold}.");
            }
        });
    }

    private static void WithTemporaryProject(Action<string> action)
    {
        var rootPath = Path.Combine(
            Path.GetTempPath(),
            "oop-design-checker-tests",
            Guid.NewGuid().ToString("N"));
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
