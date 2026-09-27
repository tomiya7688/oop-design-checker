using System.Reflection;
using OopDesignChecker.Application;
using OopDesignChecker.Output;

namespace OopDesignChecker;

public static class CheckerCommandLine
{
    public static int Run(IReadOnlyList<string> args)
    {
        if (args.Count == 1 && args[0] is "--version" or "-V")
        {
            Console.WriteLine(GetVersion());
            return 0;
        }

        var requestedLanguage = CommandLineOptionsParser.ResolveRequestedLanguage(args);
        var optionsResult = CommandLineOptionsParser.Parse(args);
        if (!optionsResult.IsSuccess)
        {
            Console.Error.WriteLine(optionsResult.ErrorMessage);
            Console.Error.WriteLine(CommandLineOptionsParser.Usage(requestedLanguage));
            return 2;
        }

        var options = optionsResult.Options!;
        var diagnosticWriter = DiagnosticWriterFactory.Create(
            options.OutputFormat,
            options.OutputPath,
            options.Language
        );
        var application = new CheckerApplication(diagnosticWriter);
        return application.Run(options);
    }

    private static string GetVersion()
    {
        var assembly = typeof(CheckerCommandLine).Assembly;
        var informationalVersion = assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()
            ?.InformationalVersion;

        if (!string.IsNullOrWhiteSpace(informationalVersion))
        {
            return informationalVersion.Split('+', 2)[0];
        }

        return assembly.GetName().Version?.ToString(3) ?? "unknown";
    }
}
