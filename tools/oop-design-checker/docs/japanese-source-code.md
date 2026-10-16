# C# 実装の日本語識別子・コメント方針

## 背景と目的

このcheckerの解析処理とアプリケーションは主にC#で実装しています。C#ではUnicode文字を識別子に使えるため、読み手が意味を理解しやすくなる箇所では、実装者が管理する内部の型名・メンバー名・変数名を日本語で記述できます。

識別子を日本語にする目的は、処理の意図をコードから読み取りやすくすることです。日本語への置き換えで意味が曖昧になる場合や、外部との契約になる名前は変更しません。

## 日本語にする範囲

- `private`、`internal` の型・メンバー・ローカル変数・引数は、意味が明確になる場合に日本語で命名します。
- 実装者が管理するコードコメントは日本語で書き、[JSON-like Comment Outs](https://github.com/tomiya7688/Json-like-comment-outs) の形式に従います。
- 型名には対象や役割を表す名詞を使います。メソッド名には処理内容を表す動詞を使います。変数名には値の意味を表す名詞を使います。
- 日本語をローマ字に置き換えた識別子は使いません。
- 日本語にすると意味が伝わりにくい標準用語は、一般に使われている英語のままにします。

## 英語名を維持する範囲

次の名前は外部契約または既存の利用者との接点なので維持します。

- `public` なCLR型・メンバーと、別assemblyとの共有契約
- namespace、assembly、package、source file、directory の名前
- CLI option、設定ファイルのkey、JSON/SARIF/GitHub annotationのfield、OOP rule ID
- UI automation selectorとaccessibility name
- C#の予約語、.NET/Roslyn/Avaloniaなどのframework・library API

日本語にした識別子でも、Unicode識別子の標準的な正規化形式NFCを使います。コード生成ファイルや外部仕様が定める名前も変更しません。

## 確認方法

`tools/oop-design-checker` を作業ディレクトリとして、次のコマンドでbuild、tests、整形を確認します。

```powershell
dotnet build OopDesignChecker.sln -c Release -p:EnableNETAnalyzers=true -p:AnalysisLevel=latest-recommended -p:EnforceCodeStyleInBuild=true -p:TreatWarningsAsErrors=true
dotnet run --project tests/OopDesignChecker.Tests/OopDesignChecker.Tests.csproj -c Release
dotnet csharpier check .
```

GitHub ActionsのWindows・Linux・macOSでも確認します。日本語識別子の変更で診断内容、CLI出力、設定値、JSON/SARIF形式、UI automation契約を変更してはいけません。

## 最終監査で維持する名前

最終監査では、製品コードの自主管理する非公開識別子と、テスト実装の型・メソッド・引数・ローカル変数を確認します。次の名前は役割や互換性のため維持します。

- `Program.Main` は.NETの実行開始点です。`Program` 型も実行開始用の慣例名として維持します。
- `BuildAvaloniaApp` はAvaloniaのテストホストを構築する慣例名です。`TestAppBuilder.cs` とテストのファイル名も変更しません。
- `CSharp`、`MSBuild`、`Slnx`、インターフェースを示す `I` は、一般的な技術名またはC#の命名慣例として維持します。使用箇所は `Analysis/CSharpProjectLoader.cs`、`Analysis/IProjectLoader.cs`、`Core/IAnalysisRule.cs` です。
- .NET、Roslyn、AvaloniaのAPI名と名前付き引数は、参照先の公開契約に合わせます。例: `Console.Error`、`DiagnosticSeverity.Error`、`Directory.Delete(recursive:)`、`CSharpSyntaxTree.ParseText(path:)`、`OpenSolutionAsync(cancellationToken:)`、`Write(verbose:)`。
- `tests/fixtures/release-validation-csharp/` とテスト内のコード断片は、解析器が検査する入力です。英語の型名・メンバー名を製品コードの識別子変更として書き換えません。
- 公開CLR API、CLI、設定、JSON/SARIF、UI automationの名前は既存契約を維持します。
