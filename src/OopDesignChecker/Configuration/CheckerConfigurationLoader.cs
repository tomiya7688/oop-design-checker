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

    private static string? ResolveConfigurationPath(string targetPath, string? explicitPath)
    {
        if (!string.IsNullOrWhiteSpace(explicitPath))
        {
            var fullPath = Path.GetFullPath(explicitPath);
            if (!File.Exists(fullPath))
            {
                throw new InvalidOperationException(
                    $"Configuration file does not exist: {fullPath}"
                );
            }

            return fullPath;
        }

        var root = File.Exists(targetPath)
            ? Path.GetDirectoryName(Path.GetFullPath(targetPath)) ?? Directory.GetCurrentDirectory()
            : Path.GetFullPath(targetPath);
        var defaultPath = Path.Combine(root, DefaultFileName);

        return File.Exists(defaultPath) ? defaultPath : null;
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
