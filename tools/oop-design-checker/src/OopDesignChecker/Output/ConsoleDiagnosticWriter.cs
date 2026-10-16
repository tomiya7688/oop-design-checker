using OopDesignChecker.Core;
using OopDesignChecker.Localization;

namespace OopDesignChecker.Output;

internal sealed class コンソール診断書き込み器 : 診断書き込み器
{
    private readonly 診断出力先 _診断出力先;
    private readonly UserInterfaceLanguage _表示言語;

    public コンソール診断書き込み器(
        string? outputPath = null,
        UserInterfaceLanguage language = UserInterfaceLanguage.Japanese
    )
    {
        _診断出力先 = new 診断出力先(outputPath);
        _表示言語 = language;
    }

    public void Write(IReadOnlyList<DesignDiagnostic> 診断群, bool 詳細表示) =>
        _診断出力先.Write(書き込み先 => 診断を書き出す(書き込み先, 診断群, 詳細表示));

    private void 診断を書き出す(
        TextWriter 書き込み先,
        IReadOnlyList<DesignDiagnostic> 診断群,
        bool 詳細表示
    )
    {
        foreach (var 診断 in 診断群)
        {
            var 記号名 = 診断.SymbolName is null ? string.Empty : $" {診断.SymbolName}";
            var 本文 = DiagnosticTextLocalizer.Message(診断, _表示言語);
            書き込み先.WriteLine(
                $"{重要度を整形する(診断.Severity)} {診断.Rule.Id} "
                    + $"{診断.Location.FilePath}:{診断.Location.Line}:{診断.Location.Column}{記号名}: "
                    + 本文
            );

            if (詳細表示)
            {
                書き込み先.WriteLine($"  {DiagnosticTextLocalizer.Title(診断, _表示言語)}");
            }
        }

        var 危険数 = 診断群.Count(項目 => 項目.Severity == DesignDiagnosticSeverity.Danger);
        var 警告数 = 診断群.Count(項目 => 項目.Severity == DesignDiagnosticSeverity.Warning);
        var 注意数 = 診断群.Count(項目 => 項目.Severity == DesignDiagnosticSeverity.Attention);

        書き込み先.WriteLine(LocalizedText.DiagnosticsSummary(_表示言語, 危険数, 警告数, 注意数));
    }

    private static string 重要度を整形する(DesignDiagnosticSeverity 重要度) =>
        重要度 switch
        {
            DesignDiagnosticSeverity.Attention => "ATTN",
            DesignDiagnosticSeverity.Warning => "WARN",
            DesignDiagnosticSeverity.Danger => "DANGER",
            _ => throw new ArgumentOutOfRangeException(nameof(重要度), 重要度, null),
        };
}
