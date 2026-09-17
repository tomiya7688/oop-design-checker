using OopDesignChecker.Application;
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
        var diagnostic = OutputFormatSmokeTests.CreateSampleDiagnosticForLocalization();
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

    private static string CaptureWriter(
        UserInterfaceLanguage language,
        OopDesignChecker.Core.DesignDiagnostic diagnostic
    )
    {
        var path = Path.Combine(Path.GetTempPath(), $"oop-localization-{Guid.NewGuid():N}.txt");
        try
        {
            new ConsoleDiagnosticWriter(path, language).Write([diagnostic], verbose: false);
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
