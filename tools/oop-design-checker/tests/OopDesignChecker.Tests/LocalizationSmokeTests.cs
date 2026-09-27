using OopDesignChecker.Application;
using OopDesignChecker.Core;
using OopDesignChecker.Localization;
using OopDesignChecker.Output;

namespace OopDesignChecker.Tests;

internal static class LocalizationSmokeTests
{
    public static void Run()
    {
        JapaneseIsDefault();
        EnglishCanBeSelected();
        InvalidLanguageIsRejected();
        TextSummaryIsLocalized();
        DiagnosticTitleAndMessageAreLocalized();
        MachineReadableDiagnosticsRemainCanonicalEnglish();
    }

    private static void JapaneseIsDefault()
    {
        var result = CommandLineOptionsParser.Parse(["sample.cs"]);
        if (
            !result.IsSuccess
            || result.Options?.Language != UserInterfaceLanguage.Japanese
            || !CommandLineOptionsParser
                .Usage(UserInterfaceLanguage.Japanese)
                .Contains("使用方法", StringComparison.Ordinal)
        )
        {
            throw new InvalidOperationException("Japanese must be the default UI language.");
        }
    }

    private static void EnglishCanBeSelected()
    {
        var result = CommandLineOptionsParser.Parse(["sample.cs", "--language", "en"]);
        if (
            !result.IsSuccess
            || result.Options?.Language != UserInterfaceLanguage.English
            || !CommandLineOptionsParser
                .Usage(UserInterfaceLanguage.English)
                .StartsWith("Usage:", StringComparison.Ordinal)
        )
        {
            throw new InvalidOperationException("--language en must select English UI text.");
        }
    }

    private static void InvalidLanguageIsRejected()
    {
        var result = CommandLineOptionsParser.Parse(["sample.cs", "--language", "xx"]);
        if (result.IsSuccess)
        {
            throw new InvalidOperationException("Unsupported UI languages must be rejected.");
        }
    }

    private static void TextSummaryIsLocalized()
    {
        var diagnostic = new DesignDiagnostic(
            new RuleDescriptor("OOP999", "Sample rule", DesignDiagnosticSeverity.Warning),
            DesignDiagnosticSeverity.Warning,
            "Sample diagnostic",
            "Sample.Member",
            new SourceLocation(Path.GetFullPath("sample.cs"), 3, 7)
        );
        var japanese = CaptureWriter(UserInterfaceLanguage.Japanese, diagnostic);
        var english = CaptureWriter(UserInterfaceLanguage.English, diagnostic);

        if (
            !japanese.Contains("診断: 危険 0件、警告 1件、注意 0件。", StringComparison.Ordinal)
            || !english.Contains(
                "Diagnostics: 0 danger, 1 warning(s), 0 attention.",
                StringComparison.Ordinal
            )
        )
        {
            throw new InvalidOperationException("Text diagnostic summaries were not localized.");
        }
    }

    private static void DiagnosticTitleAndMessageAreLocalized()
    {
        var diagnostic = new DesignDiagnostic(
            new RuleDescriptor(
                "OOP304",
                "Excessive inheritance depth",
                DesignDiagnosticSeverity.Attention
            ),
            DesignDiagnosticSeverity.Attention,
            "Project-owned inheritance depth is 4.",
            "Sample.DeepType",
            new SourceLocation(Path.GetFullPath("deep.cs"), 12, 3)
        );

        var japanese = CaptureWriter(UserInterfaceLanguage.Japanese, diagnostic, verbose: true);
        var english = CaptureWriter(UserInterfaceLanguage.English, diagnostic, verbose: true);

        if (
            !japanese.Contains("継承階層が深すぎる", StringComparison.Ordinal)
            || !japanese.Contains(
                "プロジェクト内の継承階層が深くなっています",
                StringComparison.Ordinal
            )
            || !english.Contains("Excessive inheritance depth", StringComparison.Ordinal)
            || !english.Contains("Project-owned inheritance depth is 4.", StringComparison.Ordinal)
        )
        {
            throw new InvalidOperationException(
                "Human-readable diagnostic title/message localization did not switch cleanly."
            );
        }
    }

    private static void MachineReadableDiagnosticsRemainCanonicalEnglish()
    {
        var diagnostic = new DesignDiagnostic(
            new RuleDescriptor(
                "OOP304",
                "Excessive inheritance depth",
                DesignDiagnosticSeverity.Attention
            ),
            DesignDiagnosticSeverity.Attention,
            "Project-owned inheritance depth is 4.",
            "Sample.DeepType",
            new SourceLocation(Path.GetFullPath("deep.cs"), 12, 3)
        );
        var path = Path.Combine(Path.GetTempPath(), $"oop-localization-{Guid.NewGuid():N}.json");

        try
        {
            new JsonDiagnosticWriter(path).Write([diagnostic], verbose: true);
            var json = File.ReadAllText(path);
            if (
                !json.Contains("Excessive inheritance depth", StringComparison.Ordinal)
                || !json.Contains("Project-owned inheritance depth is 4.", StringComparison.Ordinal)
                || json.Contains("継承階層が深すぎる", StringComparison.Ordinal)
            )
            {
                throw new InvalidOperationException(
                    "Machine-readable diagnostic text must remain canonical and language-stable."
                );
            }
        }
        finally
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
    }

    private static string CaptureWriter(
        UserInterfaceLanguage language,
        DesignDiagnostic diagnostic,
        bool verbose = false
    )
    {
        var path = Path.Combine(Path.GetTempPath(), $"oop-localization-{Guid.NewGuid():N}.txt");
        try
        {
            new ConsoleDiagnosticWriter(path, language).Write([diagnostic], verbose);
            return File.ReadAllText(path);
        }
        finally
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
    }
}
