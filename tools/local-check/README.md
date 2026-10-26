# PR前のローカル確認

## 背景と目的

PRを作成する前に、差分の初歩的な不具合を短い検査で確認します。起動ポイントはリポジトリルートの `check_local.py` 一つです。既存のformatter、solution、テストを内部から呼び出します。

## 実行方法

リポジトリルートで次のコマンドを実行してください。検査の組合せを指定する必要はありません。

```shell
python check_local.py
```

スクリプトはローカルにある `origin/main` とHEADの分岐点からの変更、ステージ済み・未コミット・未追跡ファイルを検査します。自分のPRの変更だけがある専用worktreeで実行してください。比較元の取得は自動では行いません。作業開始時に別途remoteを更新してください。比較元の欠落や変更0件は失敗になります。

## 事前準備

Python 3.13以降、Git、[固定formatterの準備](../formatting/README.md)が必要です。文書・YAML・JSON変更にはNode.jsと `npm ci --prefix tools/formatting --ignore-scripts --no-audit --no-fund` で準備したPrettierを使います。Pythonは監査済みの実行環境を使ってください。

C#関連変更には.NET 10 SDKを使い、初回に次を実行してください。これは環境準備であり、通常のPR前検査の起動ポイントではありません。

```shell
dotnet restore tools/oop-design-checker/OopDesignChecker.sln
dotnet tool restore --tool-manifest tools/oop-design-checker/.config/dotnet-tools.json
```

選択されたPythonテストに追加依存がある場合は、そのテストのrequirementsを事前に準備してください。依存不足はFAILです。検査スクリプトは依存のインストール、solutionのrestore、ネットワーク取得、自動整形、git add/resetを行いません。既存のC#契約テストは一時fixtureのMSBuild資産を生成します。そのrestoreは一時領域のNuGet.Configで外部feedを無効にし、準備済みのSDK・ローカルキャッシュだけを使います。

## 自動選択する検査

| 変更                                                              | 検査                                                                                                        |
| ----------------------------------------------------------------- | ----------------------------------------------------------------------------------------------------------- |
| 全変更                                                            | 分岐点との差分・indexの `git diff --check`、この入口の回帰テスト                                            |
| Python                                                            | 実行しない構文検査、固定Ruffの整形確認、同じテストディレクトリの既存unittest                                |
| C#・project・solution・props/targets・SDK/tool設定・.editorconfig | solution全体のrestoreなしBuildとanalyzer、変更C#のCSharpier、設計規則・短いCLI契約・headless UIの既存テスト |
| Markdown・YAML・JSON                                              | 固定Prettierの構文・整形確認。JSONは構文も検査します                                                        |
| TOML・MSBuild XML                                                 | 構文検査                                                                                                    |
| workflow・formatter設定・依存指定                                 | 上記に加え、既存の短いPythonテスト群                                                                        |
| 未対応形式                                                        | UNVERIFIEDとして失敗。必要な検査を追加するまでPASSにしません                                                |

削除されたファイルも関連テストやビルドの選択に使います。Pythonテストは既存の `test_*.py` を実行し、検出0件を拒否します。コマンドは1件180秒で打ち切り、後続検査と一つの要約へ進みます。

ソースやユーザーデータは書き換えません。Buildの `bin/obj` とテストの一時出力は生成します。一時出力は実行ごとに `tools/local-check/.tmp/run-*` へ分け、Gitの対象から除外します。終了時には自分の実行領域だけを削除します。ロック等で残った場合はSKIPと場所を表示し、他の実行領域には触れません。リポジトリは通常の開発時と同じく信頼できるソースを前提とします。

## 結果とCIの分担

最後にPASS / FAIL / SKIP / UNVERIFIED、理由、所要時間、再実行コマンドを表示します。必須検査が失敗・開始不能・未対応なら終了コード1になります。依存不足を修正した後も、同じ `python check_local.py` で再実行してください。

全MSBuild探索契約、Appium実画面、画像比較、複数OS/RIDの配布検査、AI API、署名・公証はCI/releaseの確認事項としてSKIP表示します。ローカルPASSはこれらの成功を意味しません。ローカルの検査結果と所要時間、SKIP/UNVERIFIED、CIの結果を分けてPRへ記録してください。

CIではこの入口の回帰テストと固定formatter確認を実行します。C#の合否は既存solution・analyzer・テスト実行体をそのまま使い、独自の判定規則を増やしません。
