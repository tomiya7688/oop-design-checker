using OopDesignChecker.Core;

namespace OopDesignChecker.Application;

internal static class CommandLineOptionsParser
{
    public const string Usage =
        "Usage: oop-design-checker [path] [--config file] [--fail-on danger|warning|attention] [--verbose]";

    public static CommandLineParseResult Parse(IReadOnlyList<string> args)
    {
        string? targetPath = null;
        string? configurationPath = null;
        DesignDiagnosticSeverity? failureThreshold = null;
        var verbose = false;

        for (var index = 0; index < args.Count; index++)
        {
            var argument = args[index];
            switch (argument)
            {
                case "--config":
                    if (!TryReadValue(args, ref index, out configurationPath))
                    {
                        return CommandLineParseResult.Failure("--config requires a file path.");
                    }
                    break;
                case "--fail-on":
                    if (
                        !TryReadValue(args, ref index, out var thresholdText)
                        || !TryParseSeverity(thresholdText, out var parsedThreshold)
                    )
                    {
                        return CommandLineParseResult.Failure(
                            "--fail-on requires danger, warning, or attention."
                        );
                    }

                    failureThreshold = parsedThreshold;
                    break;
                case "--warnings-as-errors":
                    failureThreshold = DesignDiagnosticSeverity.Warning;
                    break;
                case "--verbose":
                    verbose = true;
                    break;
                case "--help":
                case "-h":
                    return CommandLineParseResult.Failure(Usage);
                default:
                    if (argument.StartsWith('-'))
                    {
                        return CommandLineParseResult.Failure($"Unknown option: {argument}");
                    }

                    if (targetPath is not null)
                    {
                        return CommandLineParseResult.Failure(
                            "Only one target path can be specified."
                        );
                    }

                    targetPath = argument;
                    break;
            }
        }

        targetPath ??= Directory.GetCurrentDirectory();
        return CommandLineParseResult.Success(
            new CommandLineOptions(
                Path.GetFullPath(targetPath),
                configurationPath is null ? null : Path.GetFullPath(configurationPath),
                failureThreshold,
                verbose
            )
        );
    }

    private static bool TryReadValue(IReadOnlyList<string> args, ref int index, out string? value)
    {
        if (index + 1 >= args.Count)
        {
            value = null;
            return false;
        }

        index++;
        value = args[index];
        return !string.IsNullOrWhiteSpace(value);
    }

    private static bool TryParseSeverity(string? value, out DesignDiagnosticSeverity severity)
    {
        if (Enum.TryParse(value, ignoreCase: true, out severity))
        {
            return true;
        }

        severity = default;
        return false;
    }
}
