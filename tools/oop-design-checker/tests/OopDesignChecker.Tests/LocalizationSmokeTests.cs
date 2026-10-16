using OopDesignChecker.Application;
using OopDesignChecker.Core;
using OopDesignChecker.Gui;
using OopDesignChecker.Localization;
using OopDesignChecker.Output;

namespace OopDesignChecker.Tests;

internal static class 表示言語スモークテスト
{
    public static void 実行する()
    {
        日本語を既定にする();
        英語を選択できる();
        無効な言語を拒否する();
        テキスト概要を選択言語で表示する();
        診断名とメッセージを選択言語で表示する();
        日本語診断を日常的な表現にする();
        日本語の処理説明で不要な英語を避ける();
        日本語の解析エラーに読み込み器の内部情報を出さない();
        機械可読診断を英語の正規表記で維持する();
    }

    private static void 日本語を既定にする()
    {
        var 結果 = コマンドライン引数解析器.Parse(["sample.cs"]);
        if (
            !結果.IsSuccess
            || 結果.Options?.Language != UserInterfaceLanguage.Japanese
            || !コマンドライン引数解析器
                .Usage(UserInterfaceLanguage.Japanese)
                .Contains("使用方法", StringComparison.Ordinal)
        )
        {
            throw new InvalidOperationException("Japanese must be the default UI language.");
        }
    }

    private static void 英語を選択できる()
    {
        var 結果 = コマンドライン引数解析器.Parse(["sample.cs", "--language", "en"]);
        if (
            !結果.IsSuccess
            || 結果.Options?.Language != UserInterfaceLanguage.English
            || !コマンドライン引数解析器
                .Usage(UserInterfaceLanguage.English)
                .StartsWith("Usage:", StringComparison.Ordinal)
        )
        {
            throw new InvalidOperationException("--language en must select English UI text.");
        }
    }

    private static void 無効な言語を拒否する()
    {
        var 結果 = コマンドライン引数解析器.Parse(["sample.cs", "--language", "xx"]);
        if (結果.IsSuccess)
        {
            throw new InvalidOperationException("Unsupported UI languages must be rejected.");
        }
    }

    private static void テキスト概要を選択言語で表示する()
    {
        var 診断 = new DesignDiagnostic(
            new RuleDescriptor("OOP999", "Sample rule", DesignDiagnosticSeverity.Warning),
            DesignDiagnosticSeverity.Warning,
            "Sample diagnostic",
            "Sample.Member",
            new SourceLocation(Path.GetFullPath("sample.cs"), 3, 7)
        );
        var 日本語文言 = 書き込み結果を取得する(UserInterfaceLanguage.Japanese, 診断);
        var 英語文言 = 書き込み結果を取得する(UserInterfaceLanguage.English, 診断);

        if (
            !日本語文言.Contains("結果: 危険 0件、警告 1件、注意 0件。", StringComparison.Ordinal)
            || !英語文言.Contains(
                "Diagnostics: 0 danger, 1 warning(s), 0 attention.",
                StringComparison.Ordinal
            )
        )
        {
            throw new InvalidOperationException("Text diagnostic summaries were not localized.");
        }
    }

    private static void 診断名とメッセージを選択言語で表示する()
    {
        var 診断 = new DesignDiagnostic(
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

        var 日本語文言 = 書き込み結果を取得する(UserInterfaceLanguage.Japanese, 診断, true);
        var 英語文言 = 書き込み結果を取得する(UserInterfaceLanguage.English, 診断, true);

        if (
            !日本語文言.Contains("継承が深く複雑になっている", StringComparison.Ordinal)
            || !日本語文言.Contains(
                "このプロジェクトでは継承が何段階にも重なっています",
                StringComparison.Ordinal
            )
            || !英語文言.Contains("Excessive inheritance depth", StringComparison.Ordinal)
            || !英語文言.Contains("Project-owned inheritance depth is 4.", StringComparison.Ordinal)
        )
        {
            throw new InvalidOperationException(
                "Human-readable diagnostic title/message localization did not switch cleanly."
            );
        }
    }

    private static void 機械可読診断を英語の正規表記で維持する()
    {
        var 診断 = new DesignDiagnostic(
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
        var パス = Path.Combine(Path.GetTempPath(), $"oop-localization-{Guid.NewGuid():N}.json");

        try
        {
            new JSON診断書き込み器(パス).Write([診断], verbose: true);
            var JSON文字列 = File.ReadAllText(パス);
            if (
                !JSON文字列.Contains("Excessive inheritance depth", StringComparison.Ordinal)
                || !JSON文字列.Contains(
                    "Project-owned inheritance depth is 4.",
                    StringComparison.Ordinal
                )
                || JSON文字列.Contains("継承階層が深すぎる", StringComparison.Ordinal)
            )
            {
                throw new InvalidOperationException(
                    "Machine-readable diagnostic text must remain canonical and language-stable."
                );
            }
        }
        finally
        {
            if (File.Exists(パス))
            {
                File.Delete(パス);
            }
        }
    }

    private static void 日本語診断を日常的な表現にする()
    {
        var 禁止語一覧 = new[]
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

        for (var 規則番号 = 1; 規則番号 <= 3; 規則番号++)
        {
            日本語表示に禁止語がないことを確認する($"OOP{規則番号:000}", 禁止語一覧);
        }

        for (var 規則番号 = 101; 規則番号 <= 108; 規則番号++)
        {
            日本語表示に禁止語がないことを確認する($"OOP{規則番号:000}", 禁止語一覧);
        }

        日本語表示に禁止語がないことを確認する("OOP201", 禁止語一覧);

        for (var 規則番号 = 301; 規則番号 <= 307; 規則番号++)
        {
            日本語表示に禁止語がないことを確認する($"OOP{規則番号:000}", 禁止語一覧);
        }

        for (var 規則番号 = 401; 規則番号 <= 405; 規則番号++)
        {
            日本語表示に禁止語がないことを確認する($"OOP{規則番号:000}", 禁止語一覧);
        }

        var 型分岐 = 診断を作る("OOP002");
        var 公開状態 = 診断を作る("OOP106");
        var 深い辿り = 診断を作る("OOP404");
        if (
            DiagnosticTextLocalizer.Message(型分岐, UserInterfaceLanguage.Japanese)
                != "種類ごとの分岐が繰り返されています。各種類に処理を持たせて、呼び出し側の分岐を減らせないか確認してください。"
            || DiagnosticTextLocalizer.Message(公開状態, UserInterfaceLanguage.Japanese)
                != "外部から内部の値を直接変更できる状態です。値の変更方法をこのオブジェクト側で管理できないか確認してください。"
            || DiagnosticTextLocalizer.Message(深い辿り, UserInterfaceLanguage.Japanese)
                != "別のオブジェクトの中を何段階もたどって処理しています。相手の内部構造を知らずに済む形にできないか確認してください。"
        )
        {
            throw new InvalidOperationException(
                "Japanese diagnostics do not follow the plain-language policy."
            );
        }
    }

    private static void 日本語の処理説明で不要な英語を避ける()
    {
        var 用語一覧 = new[]
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
        var メッセージ一覧 = new[]
        {
            LocalizedText.ConfigRequiresPath(UserInterfaceLanguage.Japanese),
            LocalizedText.OutputRequiresPath(UserInterfaceLanguage.Japanese),
            LocalizedText.UnknownOption(UserInterfaceLanguage.Japanese, "--unknown"),
            LocalizedText.OnlyOneTarget(UserInterfaceLanguage.Japanese),
            GUI文言.取得する(GUI文言キー.対象必須, UserInterfaceLanguage.Japanese),
            GUI文言.取得する(GUI文言キー.検索入力案内, UserInterfaceLanguage.Japanese),
            GUI文言.取得する(GUI文言キー.検索メニュー, UserInterfaceLanguage.Japanese),
            GUI文言.取得する(GUI文言キー.ソースを開くボタン, UserInterfaceLanguage.Japanese),
            GUI文言.取得する(GUI文言キー.出力前解析, UserInterfaceLanguage.Japanese),
            GUI文言.取得する(GUI文言キー.クリップボード利用不可, UserInterfaceLanguage.Japanese),
            GUI文言.取得する(GUI文言キー.解析対象選択, UserInterfaceLanguage.Japanese),
            GUI文言.取得する(GUI文言キー.チェッカー設定選択, UserInterfaceLanguage.Japanese),
        };

        if (
            メッセージ一覧.Any(メッセージ =>
                用語一覧.Any(用語 => メッセージ.Contains(用語, StringComparison.OrdinalIgnoreCase))
            )
        )
        {
            throw new InvalidOperationException(
                "Japanese operation text contains unnecessary English terms."
            );
        }
    }

    private static void 日本語の解析エラーに読み込み器の内部情報を出さない()
    {
        const string 読み込みエラー内容 =
            "Target contains 3 C# projects. Use project-set analysis for multi-project targets.";
        var 日本語文言 = LocalizedText.AnalysisFailed(
            UserInterfaceLanguage.Japanese,
            読み込みエラー内容
        );
        var 英語文言 = LocalizedText.AnalysisFailed(
            UserInterfaceLanguage.English,
            読み込みエラー内容
        );

        if (
            日本語文言.Contains("Target contains", StringComparison.Ordinal)
            || 日本語文言.Contains("project-set", StringComparison.Ordinal)
            || !日本語文言.Contains("確認してください", StringComparison.Ordinal)
            || !英語文言.Contains(読み込みエラー内容, StringComparison.Ordinal)
        )
        {
            throw new InvalidOperationException(
                "Analysis errors did not follow the selected language policy."
            );
        }
    }

    private static void 日本語表示に禁止語がないことを確認する(
        string 規則ID,
        IReadOnlyList<string> 用語一覧
    )
    {
        var 診断 = 診断を作る(規則ID);
        var テキスト =
            DiagnosticTextLocalizer.Title(診断, UserInterfaceLanguage.Japanese)
            + DiagnosticTextLocalizer.Message(診断, UserInterfaceLanguage.Japanese);

        if (用語一覧.Any(用語 => テキスト.Contains(用語, StringComparison.OrdinalIgnoreCase)))
        {
            throw new InvalidOperationException(
                $"Japanese text for {規則ID} contains unnecessary jargon."
            );
        }
    }

    private static DesignDiagnostic 診断を作る(string 規則ID) =>
        new(
            new RuleDescriptor(規則ID, $"English title {規則ID}", DesignDiagnosticSeverity.Warning),
            DesignDiagnosticSeverity.Warning,
            $"English diagnostic {規則ID}",
            "Sample.Member",
            new SourceLocation("sample.cs", 1, 1)
        );

    private static string 書き込み結果を取得する(
        UserInterfaceLanguage 言語,
        DesignDiagnostic 診断,
        bool 詳細表示 = false
    )
    {
        var パス = Path.Combine(Path.GetTempPath(), $"oop-localization-{Guid.NewGuid():N}.txt");
        try
        {
            new コンソール診断書き込み器(パス, 言語).Write([診断], 詳細表示);
            return File.ReadAllText(パス);
        }
        finally
        {
            if (File.Exists(パス))
            {
                File.Delete(パス);
            }
        }
    }
}
