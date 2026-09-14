namespace OopDesignChecker.Application;

internal sealed record CommandLineParseResult(
    bool IsSuccess,
    CommandLineOptions? Options,
    string? ErrorMessage)
{
    public static CommandLineParseResult Success(CommandLineOptions options) =>
        new(true, options, null);

    public static CommandLineParseResult Failure(string errorMessage) =>
        new(false, null, errorMessage);
}
