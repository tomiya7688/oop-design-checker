using OopDesignChecker.Application;
using OopDesignChecker.Output;

namespace OopDesignChecker;

public static class CheckerCommandLine
{
    public static int Run(IReadOnlyList<string> args)
    {
        var optionsResult = CommandLineOptionsParser.Parse(args);
        if (!optionsResult.IsSuccess)
        {
            Console.Error.WriteLine(optionsResult.ErrorMessage);
            Console.Error.WriteLine(CommandLineOptionsParser.Usage);
            return 2;
        }

        var application = new CheckerApplication(new ConsoleDiagnosticWriter());
        return application.Run(optionsResult.Options!);
    }
}
