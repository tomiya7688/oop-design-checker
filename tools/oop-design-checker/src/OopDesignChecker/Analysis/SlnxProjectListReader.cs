using System.Xml;
using System.Xml.Linq;

namespace OopDesignChecker.Analysis;

/*
{
  "責務": {
    "Slnxプロジェクト一覧読み取り器": "SLNXのXML構造とProjectのPathを検証し、正規化した参照先一覧を返します。"
  },
  "フィールド": []
}
*/
internal static class Slnxプロジェクト一覧読み取り器
{
    /*
    {
      "責務": {
        "読み込む": "外部実体を読み込まずにXMLを検証し、入力エラーだけを対象パス付きの解析失敗へ変換します。"
      },
      "処理": [
        "外部実体を読み込まずにXMLを検証し、入力エラーだけを対象パス付きの解析失敗へ変換します。"
      ],
      "引数": {
        "ソリューションパス": "呼び出し元で正規化したSLNXの絶対パスです。",
        "キャンセル通知": "読み込みを中止する通知です。"
      },
      "戻り値": "正規化した参照先パス一覧です。",
      "エラー": "無効なXML、構造、Path、読み込み失敗を報告します。キャンセルはそのまま伝播します。"
    }
    */
    public static string[] 読み込む(string ソリューションパス, CancellationToken キャンセル通知)
    {
        try
        {
            var 文書 = XML文書を読む(ソリューションパス);
            return 参照先群を読む(文書, ソリューションパス, キャンセル通知);
        }
        catch (XmlException 例外)
        {
            throw 形式エラーを作る(
                ソリューションパス,
                $"XMLの形式が正しくありません（行 {例外.LineNumber}）。",
                $"Invalid XML (line {例外.LineNumber}).",
                例外
            );
        }
        catch (Exception 例外) when (例外 is ArgumentException or NotSupportedException)
        {
            throw 形式エラーを作る(
                ソリューションパス,
                "ProjectのPathが有効なファイルパスか確認してください。",
                "A Project Path is not a valid file path.",
                例外
            );
        }
        catch (Exception 例外) when (例外 is IOException or UnauthorizedAccessException)
        {
            throw 形式エラーを作る(
                ソリューションパス,
                "ファイルが存在し、読み取り可能か確認してください。",
                "The file could not be read. Check its existence and read permissions.",
                例外
            );
        }
    }

    /*
    {
      "責務": {
        "XML文書を読む": "SLNXで不要なDTDと外部実体を禁止してXML文書を読みます。"
      },
      "処理": [
        "SLNXで不要なDTDと外部実体を禁止してXML文書を読みます。"
      ],
      "引数": {
        "ソリューションパス": "読み取るSLNXの絶対パスです。"
      },
      "戻り値": "外部実体を参照せずに読み取った文書です。"
    }
    */
    private static XDocument XML文書を読む(string ソリューションパス)
    {
        using var 読み取り器 = XmlReader.Create(
            ソリューションパス,
            new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null }
        );
        return XDocument.Load(読み取り器);
    }

    /*
    {
      "責務": {
        "参照先群を読む": "Solutionのルートを検証し、Folderを含むProject一覧を列挙します。"
      },
      "処理": [
        "Solutionのルートを検証し、Folderを含むProject一覧を列挙します。"
      ],
      "引数": {
        "文書": "読み込み済みのXMLです。",
        "ソリューションパス": "対象SLNXの絶対パスです。",
        "キャンセル通知": "参照列挙を中止する通知です。"
      },
      "戻り値": "検証済みのプロジェクトパス一覧です。"
    }
    */
    private static string[] 参照先群を読む(
        XDocument 文書,
        string ソリューションパス,
        CancellationToken キャンセル通知
    )
    {
        キャンセル通知.ThrowIfCancellationRequested();
        if (文書.Root?.Name != XName.Get("Solution"))
        {
            throw 形式エラーを作る(
                ソリューションパス,
                "ルート要素には名前空間のないSolutionを指定してください。",
                "The root element must be Solution without an XML namespace."
            );
        }
        var 基準パス = Path.GetDirectoryName(ソリューションパス)!;
        var プロジェクトパス群 = new List<string>();
        // Folder配下のProjectも扱い、無関係なXML要素内のProjectは受け入れません。
        foreach (var 要素 in 文書.Descendants().Where(要素 => 要素.Name.LocalName == "Project"))
        {
            キャンセル通知.ThrowIfCancellationRequested();
            プロジェクトパス群.Add(参照先を読む(要素, 基準パス, ソリューションパス));
        }
        return プロジェクトパス群.ToArray();
    }

    /*
    {
      "責務": {
        "参照先を読む": "Projectの配置とPathの存在を検証し、OSに合わせた絶対パスへ正規化します。"
      },
      "処理": [
        "Projectの配置とPathの存在を検証し、OSに合わせた絶対パスへ正規化します。"
      ],
      "引数": {
        "要素": "検証するProject要素です。",
        "基準パス": "SLNXのあるディレクトリです。",
        "ソリューションパス": "エラー時に表示する対象SLNXです。"
      },
      "戻り値": "正規化した参照先の絶対パスです。"
    }
    */
    private static string 参照先を読む(XElement 要素, string 基準パス, string ソリューションパス)
    {
        if (
            要素.Name != XName.Get("Project")
            || 要素.Ancestors()
                .Any(親 => 親.Name != XName.Get("Solution") && 親.Name != XName.Get("Folder"))
        )
        {
            throw 形式エラーを作る(
                ソリューションパス,
                "ProjectはSolutionまたはFolderの配下に記述してください。",
                "Project elements must be under Solution or Folder elements without an XML namespace."
            );
        }
        var 相対パス = 要素.Attribute("Path")?.Value;
        if (string.IsNullOrWhiteSpace(相対パス))
        {
            throw 形式エラーを作る(
                ソリューションパス,
                "ProjectのPathにプロジェクトファイルの場所を指定してください。",
                "Every Project element requires a non-empty Path attribute."
            );
        }
        var 正規化パス = 相対パス.Replace('\\', Path.DirectorySeparatorChar)
            .Replace('/', Path.DirectorySeparatorChar);
        return Path.GetFullPath(Path.Combine(基準パス, 正規化パス));
    }

    /*
    {
      "責務": {
        "形式エラーを作る": "原因情報を保持し、利用者が対象のSLNXを特定できる説明を作ります。"
      },
      "処理": [
        "原因情報を保持し、利用者が対象のSLNXを特定できる説明を作ります。"
      ],
      "引数": {
        "ソリューションパス": "修正対象のSLNXです。",
        "日本語理由": "日本語の失敗理由です。",
        "英語理由": "英語の失敗理由です。",
        "原因": "元の例外がある場合に保持します。"
      },
      "戻り値": "元の原因と日英の説明を持つ入力例外です。"
    }
    */
    private static 解析入力例外 形式エラーを作る(
        string ソリューションパス,
        string 日本語理由,
        string 英語理由,
        Exception? 原因 = null
    ) =>
        new(
            $"ソリューションファイルを読み込めませんでした: {ソリューションパス}\n{日本語理由}",
            $"Invalid .slnx file '{ソリューションパス}': {英語理由}",
            原因
        );
}
