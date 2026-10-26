using OopDesignChecker.Core;
using OopDesignChecker.Localization;

namespace OopDesignChecker.Output;

/*
{
  "責務": {
    "コンソール診断書き込み器": "言語と詳細表示を保持し、診断と集計をtext形式で出力します。"
  }
}
*/
internal sealed class コンソール診断書き込み器 : 診断書き込み器
{
    private readonly 診断出力先 _診断出力先;
    private readonly UserInterfaceLanguage _表示言語;
    private readonly bool _詳細表示;

    /*
    {
      "責務": {
        "コンソール診断書き込み器": "text専用の表示設定と出力先を保持します。"
      },
      "処理": [
        "言語と詳細表示を固定して出力先を生成します。"
      ],
      "引数": {
        "outputPath": "未指定時は標準出力です。",
        "language": "診断を表示する言語です。",
        "詳細表示": "規則の見出しも出力する場合にtrueです。"
      },
      "戻り値": []
    }
    */
    public コンソール診断書き込み器(
        string? outputPath = null,
        UserInterfaceLanguage language = UserInterfaceLanguage.Japanese,
        bool 詳細表示 = false
    )
    {
        _診断出力先 = new 診断出力先(outputPath);
        _表示言語 = language;
        _詳細表示 = 詳細表示;
    }

    /*
    {
      "責務": {
        "Write": "言語と詳細表示を保持し、診断と集計をtext形式で出力します。"
      },
      "処理": [
        "保持した出力設定に従って診断一覧を書き込みます。"
      ],
      "引数": {
        "診断群": "出力する診断一覧です。"
      },
      "戻り値": []
    }
    */
    public void Write(IReadOnlyList<DesignDiagnostic> 診断群) =>
        _診断出力先.Write(書き込み先 => 診断を書き出す(書き込み先, 診断群));

    /*
    {
      "責務": {
        "診断を書き出す": "保持した表示設定で診断本文と集計を書き込みます。"
      },
      "処理": [
        "診断本文と必要な場合の見出しを出力します。",
        "重要度ごとの件数を出力します。"
      ],
      "引数": {
        "書き込み先": "textの出力先です。",
        "診断群": "出力する診断一覧です。"
      },
      "戻り値": []
    }
    */
    private void 診断を書き出す(TextWriter 書き込み先, IReadOnlyList<DesignDiagnostic> 診断群)
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

            if (_詳細表示)
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
