using System.Text.Json;
using System.Text.Json.Serialization;
using OopDesignChecker.Core;

namespace OopDesignChecker.Configuration;

public static class CheckerConfigurationJson
{
    private static readonly JsonSerializerOptions シリアライザー設定 = シリアライザー設定を作る();

    public static CheckerConfiguration Parse(string json)
    {
        try
        {
            var configuration =
                JsonSerializer.Deserialize<CheckerConfiguration>(json, シリアライザー設定)
                ?? throw new InvalidOperationException("The checker configuration is empty.");
            Validate(configuration);
            return configuration;
        }
        catch (JsonException 例外)
        {
            throw new InvalidOperationException(
                $"Invalid checker configuration JSON: {例外.Message}",
                例外
            );
        }
    }

    public static string Serialize(CheckerConfiguration configuration)
    {
        Validate(configuration);
        return JsonSerializer.Serialize(configuration, シリアライザー設定);
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

    private static JsonSerializerOptions シリアライザー設定を作る()
    {
        var 設定 = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            ReadCommentHandling = JsonCommentHandling.Skip,
            AllowTrailingCommas = true,
            WriteIndented = true,
        };
        設定.Converters.Add(
            new JsonStringEnumConverter(JsonNamingPolicy.CamelCase, allowIntegerValues: false)
        );
        return 設定;
    }
}
