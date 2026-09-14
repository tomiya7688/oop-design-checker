namespace OopDesignChecker.Application;

internal sealed record CommandLineOptions(
    string TargetPath,
    bool WarningsAsErrors,
    bool Verbose);
