using System.Text.Json;
using OopDesignChecker.Core;

namespace OopDesignChecker.Output;

/*
{
  "責務": {
    "JSON診断書き込み器": "診断と集計をJSON形式で出力します。"
  }
}
*/
internal sealed class JSON診断書き込み器 : 診断書き込み器
{
    private readonly 診断出力先 _診断出力先;

    public JSON診断書き込み器(string? outputPath = null)
    {
        _診断出力先 = new 診断出力先(outputPath);
    }

    /*
    {
      "責務": {
        "Write": "診断と集計をJSON形式で出力します。"
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
        var 文書 = new JSON診断文書(
            1,
            診断群.Select(診断項目に変換する).ToArray(),
            new JSON診断集計(
                診断群.Count(項目 => 項目.Severity == DesignDiagnosticSeverity.Danger),
                診断群.Count(項目 => 項目.Severity == DesignDiagnosticSeverity.Warning),
                診断群.Count(項目 => 項目.Severity == DesignDiagnosticSeverity.Attention)
            )
        );

        var シリアライザー設定 = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = true,
        };
        _診断出力先.Write(writer =>
            writer.Write(JsonSerializer.Serialize(文書, シリアライザー設定))
        );
    }

    private static JSON診断項目 診断項目に変換する(DesignDiagnostic 診断) =>
        new(
            診断.Rule.Id,
            診断.Rule.Title,
            診断.Severity.ToString().ToLowerInvariant(),
            診断.Message,
            診断.SymbolName,
            診断.Location.FilePath,
            診断.Location.Line,
            診断.Location.Column
        );

    private sealed record JSON診断文書(
        int Version,
        IReadOnlyList<JSON診断項目> Diagnostics,
        JSON診断集計 Summary
    );

    private sealed record JSON診断項目(
        string RuleId,
        string Title,
        string Severity,
        string Message,
        string? Symbol,
        string File,
        int Line,
        int Column
    );

    private sealed record JSON診断集計(int Danger, int Warning, int Attention);
}
