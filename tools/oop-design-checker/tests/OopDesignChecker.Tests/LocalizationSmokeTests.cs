using OopDesignChecker.Application;
using OopDesignChecker.Core;
using OopDesignChecker.Gui;
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
        JapaneseDiagnosticTextUsesEverydayLanguage();
        JapaneseOperationTextAvoidsUnneededEnglish();
        JapaneseAnalysisErrorsDoNotExposeLoaderInternals();
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
            !japanese.Contains("結果: 危険 0件、警告 1件、注意 0件。", StringComparison.Ordinal)
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
            !japanese.Contains("継承が深く複雑になっている", StringComparison.Ordinal)
            || !japanese.Contains(
                "このプロジェクトでは継承が何段階にも重なっています",
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

    private static void JapaneseDiagnosticTextUsesEverydayLanguage()
    {
        var forbiddenTerms = new[]
        {
            "具象型",
            "型コード",
            "ポリモーフィズム",
            "不変条件",
            "ライフタイム",
            "協調オブジェクト",
            "内部グラフ",
            "getter",
            "setter",
        };

        for (var ruleNumber = 1; ruleNumber <= 3; ruleNumber++)
        {
            AssertJapaneseTextHasNoTerms($"OOP{ruleNumber:000}", forbiddenTerms);
        }

        for (var ruleNumber = 101; ruleNumber <= 108; ruleNumber++)
        {
            AssertJapaneseTextHasNoTerms($"OOP{ruleNumber:000}", forbiddenTerms);
        }

        AssertJapaneseTextHasNoTerms("OOP201", forbiddenTerms);

        for (var ruleNumber = 301; ruleNumber <= 307; ruleNumber++)
        {
            AssertJapaneseTextHasNoTerms($"OOP{ruleNumber:000}", forbiddenTerms);
        }

        for (var ruleNumber = 401; ruleNumber <= 405; ruleNumber++)
        {
            AssertJapaneseTextHasNoTerms($"OOP{ruleNumber:000}", forbiddenTerms);
        }

        var typeBranching = CreateDiagnostic("OOP002");
        var exposedState = CreateDiagnostic("OOP106");
        var deepNavigation = CreateDiagnostic("OOP404");
        if (
            DiagnosticTextLocalizer.Message(typeBranching, UserInterfaceLanguage.Japanese)
                != "種類ごとの分岐が繰り返されています。各種類に処理を持たせて、呼び出し側の分岐を減らせないか確認してください。"
            || DiagnosticTextLocalizer.Message(exposedState, UserInterfaceLanguage.Japanese)
                != "外部から内部の値を直接変更できる状態です。値の変更方法をこのオブジェクト側で管理できないか確認してください。"
            || DiagnosticTextLocalizer.Message(deepNavigation, UserInterfaceLanguage.Japanese)
                != "別のオブジェクトの中を何段階もたどって処理しています。相手の内部構造を知らずに済む形にできないか確認してください。"
        )
        {
            throw new InvalidOperationException(
                "Japanese diagnostics do not follow the plain-language policy."
            );
        }
    }

    private static void JapaneseOperationTextAvoidsUnneededEnglish()
    {
        var terms = new[]
        {
            "target path",
            "option",
            "export",
            "clipboard",
            "source file",
            "sourceを",
            "platform",
            "target",
            "checker設定",
            "フォーカス",
        };
        var messages = new[]
        {
            LocalizedText.ConfigRequiresPath(UserInterfaceLanguage.Japanese),
            LocalizedText.OutputRequiresPath(UserInterfaceLanguage.Japanese),
            LocalizedText.UnknownOption(UserInterfaceLanguage.Japanese, "--unknown"),
            LocalizedText.OnlyOneTarget(UserInterfaceLanguage.Japanese),
            GuiText.Get(GuiTextKey.TargetRequired, UserInterfaceLanguage.Japanese),
            GuiText.Get(GuiTextKey.SearchPlaceholder, UserInterfaceLanguage.Japanese),
            GuiText.Get(GuiTextKey.SearchMenu, UserInterfaceLanguage.Japanese),
            GuiText.Get(GuiTextKey.OpenSourceButton, UserInterfaceLanguage.Japanese),
            GuiText.Get(GuiTextKey.RunBeforeExport, UserInterfaceLanguage.Japanese),
            GuiText.Get(GuiTextKey.ClipboardUnavailable, UserInterfaceLanguage.Japanese),
            GuiText.Get(GuiTextKey.ChooseAnalysisTarget, UserInterfaceLanguage.Japanese),
            GuiText.Get(GuiTextKey.ChooseCheckerConfiguration, UserInterfaceLanguage.Japanese),
        };

        if (
            messages.Any(message =>
                terms.Any(term => message.Contains(term, StringComparison.OrdinalIgnoreCase))
            )
        )
        {
            throw new InvalidOperationException(
                "Japanese operation text contains unnecessary English terms."
            );
        }
    }

    private static void JapaneseAnalysisErrorsDoNotExposeLoaderInternals()
    {
        const string loaderMessage =
            "Target contains 3 C# projects. Use project-set analysis for multi-project targets.";
        var japanese = LocalizedText.AnalysisFailed(UserInterfaceLanguage.Japanese, loaderMessage);
        var english = LocalizedText.AnalysisFailed(UserInterfaceLanguage.English, loaderMessage);

        if (
            japanese.Contains("Target contains", StringComparison.Ordinal)
            || japanese.Contains("project-set", StringComparison.Ordinal)
            || !japanese.Contains("確認してください", StringComparison.Ordinal)
            || !english.Contains(loaderMessage, StringComparison.Ordinal)
        )
        {
            throw new InvalidOperationException(
                "Analysis errors did not follow the selected language policy."
            );
        }
    }

    private static void AssertJapaneseTextHasNoTerms(string ruleId, IReadOnlyList<string> terms)
    {
        var diagnostic = CreateDiagnostic(ruleId);
        var text =
            DiagnosticTextLocalizer.Title(diagnostic, UserInterfaceLanguage.Japanese)
            + DiagnosticTextLocalizer.Message(diagnostic, UserInterfaceLanguage.Japanese);

        if (terms.Any(term => text.Contains(term, StringComparison.OrdinalIgnoreCase)))
        {
            throw new InvalidOperationException(
                $"Japanese text for {ruleId} contains unnecessary jargon."
            );
        }
    }

    private static DesignDiagnostic CreateDiagnostic(string ruleId) =>
        new(
            new RuleDescriptor(ruleId, $"English title {ruleId}", DesignDiagnosticSeverity.Warning),
            DesignDiagnosticSeverity.Warning,
            $"English diagnostic {ruleId}",
            "Sample.Member",
            new SourceLocation("sample.cs", 1, 1)
        );

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
