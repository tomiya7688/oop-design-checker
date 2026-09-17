using System.Reflection;
using OopDesignChecker.Application;
using OopDesignChecker.Localization;
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

        var requestedLanguage = DetectRequestedLanguage(args);
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

    private static UserInterfaceLanguage DetectRequestedLanguage(IReadOnlyList<string> args)
    {
        for (var index = 0; index + 1 < args.Count; index++)
        {
            if (
                args[index] == "--language"
                && UserInterfaceText.TryParseLanguage(args[index + 1], out var language)
            )
            {
                return language;
            }
        }

        return UserInterfaceText.DefaultLanguage;
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
