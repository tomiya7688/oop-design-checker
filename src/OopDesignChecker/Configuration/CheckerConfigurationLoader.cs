using System.Text.Json;
using System.Text.Json.Serialization;

namespace OopDesignChecker.Configuration;

internal static class CheckerConfigurationLoader
{
    public const string DefaultFileName = "oop-design-checker.json";

    private static readonly JsonSerializerOptions JsonOptions = CreateJsonOptions();

    public static LoadedCheckerConfiguration Load(string targetPath, string? explicitPath)
    {
        var configurationPath = ResolveConfigurationPath(targetPath, explicitPath);
        if (configurationPath is null)
        {
            return new LoadedCheckerConfiguration(new CheckerConfiguration(), null);
        }

        try
        {
            var json = File.ReadAllText(configurationPath);
            var configuration =
                JsonSerializer.Deserialize<CheckerConfiguration>(json, JsonOptions)
                ?? throw new InvalidOperationException("The checker configuration is empty.");

            Validate(configuration, configurationPath);
            return new LoadedCheckerConfiguration(configuration, configurationPath);
        }
        catch (JsonException exception)
        {
            throw new InvalidOperationException(
                $"Invalid checker configuration '{configurationPath}': {exception.Message}",
                exception
            );
        }
    }

    private static void Validate(CheckerConfiguration configuration, string configurationPath)
    {
        if (configuration.RuleSettings.Oop304.WarningDepth < 1)
        {
            throw new InvalidOperationException(
                $"Invalid checker configuration '{configurationPath}': ruleSettings.oop304.warningDepth must be at least 1."
            );
        }
    }

    private static string? ResolveConfigurationPath(string targetPath, string? explicitPath)
    {
        var targetDirectory = GetTargetDirectory(targetPath);
        if (!string.IsNullOrWhiteSpace(explicitPath))
        {
            return ResolveExplicitConfigurationPath(explicitPath, targetDirectory);
        }

        return FindNearestDefaultConfiguration(targetDirectory);
    }

    private static string ResolveExplicitConfigurationPath(
        string explicitPath,
        string targetDirectory
    )
    {
        if (Path.IsPathRooted(explicitPath))
        {
            var fullPath = Path.GetFullPath(explicitPath);
            EnsureConfigurationExists(fullPath);
            return fullPath;
        }

        var currentDirectoryPath = Path.GetFullPath(explicitPath, Directory.GetCurrentDirectory());
        if (File.Exists(currentDirectoryPath))
        {
            return currentDirectoryPath;
        }

        var targetRelativePath = Path.GetFullPath(explicitPath, targetDirectory);
        if (File.Exists(targetRelativePath))
        {
            return targetRelativePath;
        }

        throw new InvalidOperationException(
            $"Configuration file does not exist. Checked: {currentDirectoryPath}; {targetRelativePath}"
        );
    }

    private static string? FindNearestDefaultConfiguration(string targetDirectory)
    {
        var directory = new DirectoryInfo(targetDirectory);
        while (directory is not null)
        {
            var candidate = Path.Combine(directory.FullName, DefaultFileName);
            if (File.Exists(candidate))
            {
                return candidate;
            }

            directory = directory.Parent;
        }

        return null;
    }

    private static string GetTargetDirectory(string targetPath)
    {
        var fullTargetPath = Path.GetFullPath(targetPath);
        if (Directory.Exists(fullTargetPath))
        {
            return fullTargetPath;
        }

        return Path.GetDirectoryName(fullTargetPath) ?? Directory.GetCurrentDirectory();
    }

    private static void EnsureConfigurationExists(string path)
    {
        if (!File.Exists(path))
        {
            throw new InvalidOperationException($"Configuration file does not exist: {path}");
        }
    }

    private static JsonSerializerOptions CreateJsonOptions()
    {
        var options = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            ReadCommentHandling = JsonCommentHandling.Skip,
            AllowTrailingCommas = true,
        };
        options.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase));
        return options;
    }
}

internal sealed record LoadedCheckerConfiguration(CheckerConfiguration Configuration, string? Path);
