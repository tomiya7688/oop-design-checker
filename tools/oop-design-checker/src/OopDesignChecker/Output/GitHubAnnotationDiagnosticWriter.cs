using OopDesignChecker.Core;

namespace OopDesignChecker.Output;

/*
{
  "責務": {
    "GitHub注釈診断書き込み器": "診断をGitHub注釈として標準出力に書き込みます。"
  }
}
*/
internal sealed class GitHub注釈診断書き込み器 : 診断書き込み器
{
    /*
    {
      "責務": {
        "Write": "診断をGitHub注釈として標準出力に書き込みます。"
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
    public void Write(IReadOnlyList<DesignDiagnostic> 診断群)
    {
        foreach (var 診断 in 診断群)
        {
            var 注釈 = 注釈重要度を整形する(診断.Severity);
            var ファイル = 注釈属性値をエスケープする(診断.Location.FilePath);
            var 見出し = 注釈属性値をエスケープする($"{診断.Rule.Id} {診断.Rule.Title}");
            var 本文 = 注釈本文をエスケープする(
                診断.SymbolName is null ? 診断.Message : $"{診断.SymbolName}: {診断.Message}"
            );

            Console.WriteLine(
                $"::{注釈} file={ファイル},line={診断.Location.Line},col={診断.Location.Column},title={見出し}::{本文}"
            );
        }
    }

    private static string 注釈重要度を整形する(DesignDiagnosticSeverity 重要度) =>
        重要度 switch
        {
            DesignDiagnosticSeverity.Danger => "error",
            DesignDiagnosticSeverity.Warning => "warning",
            DesignDiagnosticSeverity.Attention => "notice",
            _ => throw new ArgumentOutOfRangeException(nameof(重要度), 重要度, null),
        };

    private static string 注釈属性値をエスケープする(string 値) =>
        注釈本文をエスケープする(値)
            .Replace(":", "%3A", StringComparison.Ordinal)
            .Replace(",", "%2C", StringComparison.Ordinal);

    private static string 注釈本文をエスケープする(string 値) =>
        値.Replace("%", "%25", StringComparison.Ordinal)
            .Replace("\r", "%0D", StringComparison.Ordinal)
            .Replace("\n", "%0A", StringComparison.Ordinal);
}
