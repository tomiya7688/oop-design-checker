using System.Text.Json;
using OopDesignChecker.Analysis;
using OopDesignChecker.Core;

namespace OopDesignChecker.Output;

/*
{
  "責務": {
    "SARIF診断書き込み器": "診断をSARIF形式で出力します。"
  }
}
*/
internal sealed class SARIF診断書き込み器 : 診断書き込み器
{
    private readonly 診断出力先 _診断出力先;

    public SARIF診断書き込み器(string? outputPath = null)
    {
        _診断出力先 = new 診断出力先(outputPath);
    }

    /*
    {
      "責務": {
        "Write": "診断をSARIF形式で出力します。"
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
        var 規則群 = 診断群.GroupBy(診断 => 診断.Rule.Id, StringComparer.Ordinal)
            .Select(同一規則群 => 同一規則群.First())
            .OrderBy(診断 => 診断.Rule.Id, StringComparer.Ordinal)
            .Select(規則情報に変換する)
            .ToArray();

        var 実行情報 = new
        {
            tool = new { driver = new { name = "oop-design-checker", rules = 規則群 } },
            results = 診断群.Select(診断結果に変換する).ToArray(),
        };

        var 文書 = new Dictionary<string, object?>
        {
            ["version"] = "2.1.0",
            ["$schema"] = "https://json.schemastore.org/sarif-2.1.0.json",
            ["runs"] = new[] { 実行情報 },
        };
        var シリアライザー設定 = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = true,
        };

        _診断出力先.Write(writer =>
            writer.Write(JsonSerializer.Serialize(文書, シリアライザー設定))
        );
    }

    private static object 規則情報に変換する(DesignDiagnostic 診断) =>
        new
        {
            id = 診断.Rule.Id,
            name = 診断.Rule.Title,
            shortDescription = new { text = 診断.Rule.Title },
            defaultConfiguration = new { level = SARIF重要度を整形する(診断.Severity) },
        };

    private static object 診断結果に変換する(DesignDiagnostic 診断)
    {
        var 位置情報群 = 診断ファイルパス.ファイルURIを作成できる(
            診断.Location.FilePath,
            out var ファイルURI
        )
            ? new object[]
            {
                new
                {
                    physicalLocation = new
                    {
                        artifactLocation = new { uri = ファイルURI },
                        region = new
                        {
                            startLine = 診断.Location.Line,
                            startColumn = 診断.Location.Column,
                        },
                    },
                },
            }
            : [];

        return new
        {
            ruleId = 診断.Rule.Id,
            level = SARIF重要度を整形する(診断.Severity),
            message = new { text = 診断.Message },
            locations = 位置情報群,
            properties = 診断.SymbolName is null
                ? null
                : new Dictionary<string, string> { ["symbol"] = 診断.SymbolName },
        };
    }

    private static string SARIF重要度を整形する(DesignDiagnosticSeverity 重要度) =>
        重要度 switch
        {
            DesignDiagnosticSeverity.Danger => "error",
            DesignDiagnosticSeverity.Warning => "warning",
            DesignDiagnosticSeverity.Attention => "note",
            _ => throw new ArgumentOutOfRangeException(nameof(重要度), 重要度, null),
        };
}
