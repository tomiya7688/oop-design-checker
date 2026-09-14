namespace OopDesignChecker.Application;

internal static class CommandLineOptionsParser
{
    public const string Usage = "Usage: oop-design-checker [path] [--warnings-as-errors] [--verbose]";

    public static CommandLineParseResult Parse(IReadOnlyList<string> args)
    {
        string? targetPath = null;
        var warningsAsErrors = false;
        var verbose = false;

        foreach (var argument in args)
        {
            switch (argument)
            {
                case "--warnings-as-errors":
                    warningsAsErrors = true;
                    break;
                case "--verbose":
                    verbose = true;
                    break;
                case "--help":
                case "-h":
                    return CommandLineParseResult.Failure(Usage);
                default:
                    if (argument.StartsWith('-', StringComparison.Ordinal))
                    {
                        return CommandLineParseResult.Failure($"Unknown option: {argument}");
                    }

                    if (targetPath is not null)
                    {
                        return CommandLineParseResult.Failure("Only one target path can be specified.");
                    }

                    targetPath = argument;
                    break;
            }
        }

        targetPath ??= Directory.GetCurrentDirectory();
        return CommandLineParseResult.Success(
            new CommandLineOptions(Path.GetFullPath(targetPath), warningsAsErrors, verbose));
    }
}
