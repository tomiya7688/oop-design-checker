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
        TextSummaryAndDiagnosticAreLocalized();
        MachineReadableOutputKeepsStableDiagnosticText();
    }

    private static void JapaneseIsDefault()
    {
        var result = CommandLineOptionsParser.Parse(["sample.cs"]);
        if (
            !result.IsSuccess
            || result.Options?.Language != UserInterfaceLanguage.Japanese
            || !CommandLineOptionsParser
                .Usage(UserInterfaceLanguage.Japanese)
                .Contains("使い方", StringComparison.Ordinal)
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

    private static void TextSummaryAndDiagnosticAreLocalized()
    {
        var diagnostic = CreateDiagnostic();
        var japanese = CaptureTextWriter(UserInterfaceLanguage.Japanese, diagnostic);
        var english = CaptureTextWriter(UserInterfaceLanguage.English, diagnostic);

        if (
            !japanese.Contains("警告 OOP106", StringComparison.Ordinal)
            || !japanese.Contains("内部表現が外部へ直接公開", StringComparison.Ordinal)
            || !japanese.Contains("診断: 危険 0件、警告 1件、注意 0件。", StringComparison.Ordinal)
            || !english.Contains("WARN OOP106", StringComparison.Ordinal)
            || !english.Contains("Original English diagnostic", StringComparison.Ordinal)
            || !english.Contains(
                "Diagnostics: 0 danger, 1 warning(s), 0 attention.",
                StringComparison.Ordinal
            )
        )
        {
            throw new InvalidOperationException(
                "Human-readable diagnostics were not localized correctly."
            );
        }
    }

    private static void MachineReadableOutputKeepsStableDiagnosticText()
    {
        var path = Path.Combine(Path.GetTempPath(), $"oop-localization-{Guid.NewGuid():N}.json");
        try
        {
            new JsonDiagnosticWriter(path).Write([CreateDiagnostic()], verbose: false);
            var json = File.ReadAllText(path);
            if (
                !json.Contains("Original English diagnostic", StringComparison.Ordinal)
                || json.Contains("内部表現が外部へ直接公開", StringComparison.Ordinal)
            )
            {
                throw new InvalidOperationException(
                    "Machine-readable output must keep language-stable diagnostic text."
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

    private static DesignDiagnostic CreateDiagnostic() =>
        new(
            new RuleDescriptor("OOP106", "Encapsulation leak", DesignDiagnosticSeverity.Warning),
            DesignDiagnosticSeverity.Warning,
            "Original English diagnostic",
            "Sample.Member",
            new SourceLocation(Path.GetFullPath("sample.cs"), 3, 7)
        );

    private static string CaptureTextWriter(
        UserInterfaceLanguage language,
        DesignDiagnostic diagnostic
    )
    {
        var path = Path.Combine(Path.GetTempPath(), $"oop-localization-{Guid.NewGuid():N}.txt");
        try
        {
            new ConsoleDiagnosticWriter(path, language).Write([diagnostic], verbose: true);
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
