using OopDesignChecker.Core;

namespace OopDesignChecker.Application;

internal static class CommandLineOptionsParser
{
    public const string Usage =
        "Usage: oop-design-checker [path] [--config file] [--fail-on danger|warning|attention] [--format text|json|sarif|github] [--output file] [--verbose]";

    public static CommandLineParseResult Parse(IReadOnlyList<string> args)
    {
        string? targetPath = null;
        string? configurationPath = null;
        string? outputPath = null;
        DesignDiagnosticSeverity? failureThreshold = null;
        var verbose = false;
        var outputFormat = DiagnosticOutputFormat.Text;

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
                case "--format":
                    if (
                        !TryReadValue(args, ref index, out var formatText)
                        || !TryParseOutputFormat(formatText, out outputFormat)
                    )
                    {
                        return CommandLineParseResult.Failure(
                            "--format requires text, json, sarif, or github."
                        );
                    }
                    break;
                case "--output":
                    if (!TryReadValue(args, ref index, out outputPath))
                    {
                        return CommandLineParseResult.Failure("--output requires a file path.");
                    }
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

        if (outputFormat == DiagnosticOutputFormat.GitHub && outputPath is not null)
        {
            return CommandLineParseResult.Failure(
                "--output cannot be used with --format github because workflow annotations must be written to stdout."
            );
        }

        targetPath ??= Directory.GetCurrentDirectory();
        return CommandLineParseResult.Success(
            new CommandLineOptions(
                Path.GetFullPath(targetPath),
                configurationPath is null ? null : Path.GetFullPath(configurationPath),
                failureThreshold,
                verbose,
                outputFormat,
                outputPath is null ? null : Path.GetFullPath(outputPath)
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

    private static bool TryParseOutputFormat(string? value, out DiagnosticOutputFormat format)
    {
        if (Enum.TryParse(value, ignoreCase: true, out format))
        {
            return true;
        }

        format = default;
        return false;
    }
}
