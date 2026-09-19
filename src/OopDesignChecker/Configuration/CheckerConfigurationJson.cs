using System.Text.Json;
using System.Text.Json.Serialization;
using OopDesignChecker.Core;

namespace OopDesignChecker.Configuration;

public static class CheckerConfigurationJson
{
    private static readonly JsonSerializerOptions SerializerOptions = CreateSerializerOptions();

    public static CheckerConfiguration Parse(string json)
    {
        try
        {
            var configuration =
                JsonSerializer.Deserialize<CheckerConfiguration>(json, SerializerOptions)
                ?? throw new InvalidOperationException("The checker configuration is empty.");
            Validate(configuration);
            return configuration;
        }
        catch (JsonException exception)
        {
            throw new InvalidOperationException(
                $"Invalid checker configuration JSON: {exception.Message}",
                exception
            );
        }
    }

    public static string Serialize(CheckerConfiguration configuration)
    {
        Validate(configuration);
        return JsonSerializer.Serialize(configuration, SerializerOptions);
    }

    public static void Validate(CheckerConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        if (!Enum.IsDefined(configuration.FailureThreshold))
        {
            throw new InvalidOperationException("failureThreshold must be a defined severity.");
        }

        if (configuration.IgnoredPaths is null)
        {
            throw new InvalidOperationException("ignoredPaths must be an array.");
        }

        if (configuration.DisabledRules is null)
        {
            throw new InvalidOperationException("disabledRules must be an array.");
        }

        if (configuration.RuleSettings is null)
        {
            throw new InvalidOperationException("ruleSettings must be an object.");
        }

        if (configuration.RuleSettings.Oop304 is null)
        {
            throw new InvalidOperationException("ruleSettings.oop304 must be an object.");
        }

        if (configuration.RuleSettings.Oop304.WarningDepth < 1)
        {
            throw new InvalidOperationException(
                "ruleSettings.oop304.warningDepth must be at least 1."
            );
        }
    }

    private static JsonSerializerOptions CreateSerializerOptions()
    {
        var options = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            ReadCommentHandling = JsonCommentHandling.Skip,
            AllowTrailingCommas = true,
            WriteIndented = true,
        };
        options.Converters.Add(
            new JsonStringEnumConverter(JsonNamingPolicy.CamelCase, allowIntegerValues: false)
        );
        return options;
    }
}
