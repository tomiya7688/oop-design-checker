namespace OopDesignChecker.Configuration;

internal static class CheckerConfigurationLoader
{
    public const string DefaultFileName = "oop-design-checker.json";

    public static LoadedCheckerConfiguration Load(string targetPath, string? explicitPath)
    {
        var configurationPath = CheckerConfigurationPathResolver.Resolve(targetPath, explicitPath);
        if (configurationPath is null)
        {
            return new LoadedCheckerConfiguration(new CheckerConfiguration(), null);
        }

        try
        {
            var json = File.ReadAllText(configurationPath);
            var configuration = CheckerConfigurationJson.Parse(json);
            return new LoadedCheckerConfiguration(configuration, configurationPath);
        }
        catch (InvalidOperationException exception)
        {
            throw new InvalidOperationException(
                $"Invalid checker configuration '{configurationPath}': {exception.Message}",
                exception
            );
        }
    }
}

internal sealed record LoadedCheckerConfiguration(CheckerConfiguration Configuration, string? Path);
