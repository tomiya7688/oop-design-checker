using OopDesignChecker.Application;
using OopDesignChecker.Analysis;
using OopDesignChecker.Output;
using OopDesignChecker.Rules;

namespace OopDesignChecker;

internal static class Program
{
    public static int Main(string[] args)
    {
        var optionsResult = CommandLineOptionsParser.Parse(args);
        if (!optionsResult.IsSuccess)
        {
            Console.Error.WriteLine(optionsResult.ErrorMessage);
            Console.Error.WriteLine(CommandLineOptionsParser.Usage);
            return 2;
        }

        var loader = new CSharpProjectLoader();
        var rules = RuleCatalog.CreateDefault();
        var engine = new AnalysisEngine(rules);
        var writer = new ConsoleDiagnosticWriter();
        var application = new CheckerApplication(loader, engine, writer);

        return application.Run(optionsResult.Options!);
    }
}
