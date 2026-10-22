using System.Text.Json;
using OopDesignChecker.Core;
using OopDesignChecker.Output;
using Xunit;

namespace OopDesignChecker.ContractTests;

/*
{
  "責務": {"出力契約テスト": "共有writerの形式選択と公開ファイル保存APIの能力制約、出力互換性を検証します。"}
}
*/
public sealed class 出力契約テスト
{
    /*
    {
      "責務": {"共有形式に対応するwriterを生成する": "共有出力形式の全値が対応するwriterを選ぶことを確認します。"},
      "処理": ["共有形式から生成したwriterの型を確認します。"],
      "引数": {"形式名": "共有出力形式の名前です。", "期待型": "対応するwriterの型です。"},
      "戻り値": []
    }
    */
    [Theory]
    [InlineData("Text", typeof(コンソール診断書き込み器))]
    [InlineData("Json", typeof(JSON診断書き込み器))]
    [InlineData("Sarif", typeof(SARIF診断書き込み器))]
    [InlineData("GitHub", typeof(GitHub注釈診断書き込み器))]
    public void 共有形式に対応するwriterを生成する(string 形式名, Type 期待型) =>
        Assert.IsType(期待型, 診断書き込み器生成器.Create(Enum.Parse<診断出力形式>(形式名), null));

    /*
    {
      "責務": {"保存形式はJSONとSARIFだけに対応する": "保存専用の契約から対応するwriterを生成することを確認します。"},
      "処理": ["保存形式から生成したwriterの型を確認します。"],
      "引数": {"形式": "公開の保存形式です。", "期待型": "対応するwriterの型です。"},
      "戻り値": []
    }
    */
    [Theory]
    [InlineData(DiagnosticExportFormat.Json, typeof(JSON診断書き込み器))]
    [InlineData(DiagnosticExportFormat.Sarif, typeof(SARIF診断書き込み器))]
    public void 保存形式はJSONとSARIFだけに対応する(DiagnosticExportFormat 形式, Type 期待型) =>
        Assert.IsType(期待型, 診断書き込み器生成器.ファイル保存用を作る(形式, "unused.json"));

    /*
    {
      "責務": {"未定義の共有形式を拒否する": "共有形式の不正な値でwriterを生成しないことを確認します。"},
      "処理": ["未定義値を渡し、例外の引数名を確認します。"],
      "引数": {"形式": "共有形式に定義されていない値です。"},
      "戻り値": []
    }
    */
    [Theory]
    [InlineData(-1)]
    [InlineData(4)]
    [InlineData(int.MaxValue)]
    public void 未定義の共有形式を拒否する(int 形式) =>
        Assert.Equal(
            "format",
            Assert
                .Throws<ArgumentOutOfRangeException>(() =>
                    診断書き込み器生成器.Create((診断出力形式)形式, null)
                )
                .ParamName
        );

    /*
    {
      "責務": {"未定義の保存形式で既存ファイルを上書きしない": "保存形式の拒否を、既存ファイルへ書き込む前に維持します。"},
      "処理": ["既存ファイルに対して未定義の保存形式を指定します。", "例外とファイル内容の維持を確認します。"],
      "引数": {"形式": "公開の保存形式に定義されていない値です。"},
      "戻り値": []
    }
    */
    [Theory]
    [InlineData(-1)]
    [InlineData(2)]
    [InlineData(int.MaxValue)]
    public void 未定義の保存形式で既存ファイルを上書きしない(int 形式)
    {
        using var 環境 = new 一時解析環境();
        var 出力パス = 環境.ファイルを書く("existing.json", "保存前の内容");

        var 例外 = Assert.Throws<ArgumentOutOfRangeException>(() =>
            DiagnosticExportService.Export([], 出力パス, (DiagnosticExportFormat)形式)
        );

        Assert.Equal("format", 例外.ParamName);
        Assert.Equal("保存前の内容", File.ReadAllText(出力パス));
    }

    /*
    {
      "責務": {"公開保存APIは共有writerと同じ文書を保存する": "公開APIの保存内容とJSONまたはSARIFのschemaを維持します。"},
      "処理": ["同じ診断を公開APIと共有writerから別々に保存します。", "文書全体と形式ごとのversionを確認します。"],
      "引数": {"形式": "公開の保存形式です。", "共有形式名": "対応する共有出力形式名です。", "期待version": "schemaのversionです。"},
      "戻り値": []
    }
    */
    [Theory]
    [InlineData(DiagnosticExportFormat.Json, "Json", "1")]
    [InlineData(DiagnosticExportFormat.Sarif, "Sarif", "2.1.0")]
    public void 公開保存APIは共有writerと同じ文書を保存する(
        DiagnosticExportFormat 形式,
        string 共有形式名,
        string 期待version
    )
    {
        using var 環境 = new 一時解析環境();
        var 出力パス = Path.Combine(環境.ルート, "nested", "export.json");
        var 比較パス = Path.Combine(環境.ルート, "shared.json");
        DesignDiagnostic[] 診断群 =
        [
            new(
                new RuleDescriptor("OOP999", "Sample rule", DesignDiagnosticSeverity.Warning),
                DesignDiagnosticSeverity.Warning,
                "first%line\nsecond",
                "Sample.Member",
                new SourceLocation(Path.Combine(環境.ルート, "sample.cs"), 3, 7)
            ),
        ];

        DiagnosticExportService.Export(診断群, 出力パス, 形式);
        診断書き込み器生成器.Create(Enum.Parse<診断出力形式>(共有形式名), 比較パス).Write(診断群);

        var 本文 = File.ReadAllText(出力パス);
        Assert.Equal(File.ReadAllText(比較パス), 本文);
        using var 文書 = JsonDocument.Parse(本文);
        Assert.Equal(期待version, 文書.RootElement.GetProperty("version").ToString());
    }

    /*
    {
      "責務": {"共有形式をOutputが所有し公開保存形式を維持する": "形式の所有元と公開enumの名前・数値を固定します。"},
      "処理": ["共有形式のnamespaceと全値を確認します。", "公開保存形式の完全名と既存の値を確認します。"],
      "引数": [],
      "戻り値": []
    }
    */
    [Fact]
    public void 共有形式をOutputが所有し公開保存形式を維持する()
    {
        Assert.Equal("OopDesignChecker.Output", typeof(診断出力形式).Namespace);
        Assert.Equal(["Text", "Json", "Sarif", "GitHub"], Enum.GetNames<診断出力形式>());
        Assert.Equal(
            "OopDesignChecker.Output.DiagnosticExportFormat",
            typeof(DiagnosticExportFormat).FullName
        );
        Assert.Equal(["Json", "Sarif"], Enum.GetNames<DiagnosticExportFormat>());
        Assert.Equal(0, (int)DiagnosticExportFormat.Json);
        Assert.Equal(1, (int)DiagnosticExportFormat.Sarif);
    }

    /*
    {
      "責務": {"公開保存APIはnullの診断一覧を先に拒否する": "既存の入力検証順序と例外の引数名を維持します。"},
      "処理": ["診断一覧と出力先を同時に不正にし、診断一覧を先に拒否することを確認します。"],
      "引数": [],
      "戻り値": []
    }
    */
    [Fact]
    public void 公開保存APIはnullの診断一覧を先に拒否する() =>
        Assert.Equal(
            "diagnostics",
            Assert
                .Throws<ArgumentNullException>(() =>
                    DiagnosticExportService.Export(null!, "", (DiagnosticExportFormat)(-1))
                )
                .ParamName
        );

    /*
    {
      "責務": {"公開保存APIは不正な形式より空の保存先を先に拒否する": "保存先の入力検証とその順序を維持します。"},
      "処理": ["空の保存先と未定義形式を渡し、保存先に関する例外を確認します。"],
      "引数": {"保存先": "null、空文字または空白の保存先です。"},
      "戻り値": []
    }
    */
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    public void 公開保存APIは不正な形式より空の保存先を先に拒否する(string? 保存先)
    {
        var 例外 = Assert.ThrowsAny<ArgumentException>(() =>
            DiagnosticExportService.Export([], 保存先!, (DiagnosticExportFormat)(-1))
        );
        Assert.Equal("outputPath", 例外.ParamName);
    }
}
