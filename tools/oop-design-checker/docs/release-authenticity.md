# 配布物の真正性と公開停止

## 背景と目的

#231では、最終成果物が期待するリポジトリ・workflow・commitから作られたものか検証できるようにします。チェックサムだけでは、配布物とチェックサムを一緒に差し替えられた場合の発行元を確認できません。

**現在は公開停止中です。** Windowsの信頼されたAuthenticode署名・RFC 3161 timestampと、macOSのDeveloper ID署名・notarizationは未導入です。archiveのprovenanceが成功しても、GitHub Releaseの作成ステップは必ず失敗します。環境変数や入力で停止を解除する機能は設けません。

## 対象となる成果物

release.ymlはCUIとGUIをそれぞれ次の6 RIDへビルドします。

| RID                     | 最終アーカイブ |
| ----------------------- | -------------- |
| win-x64 / win-arm64     | ZIP            |
| linux-x64 / linux-arm64 | TAR.GZ         |
| osx-x64 / osx-arm64     | TAR.GZ         |

計12アーカイブには、実行ファイル、Roslyn BuildHost、参照assembly、設定例、日本語・英語文書が含まれます。個別.sha256ファイル12件と統合SHA256SUMS.txtを含め、最終公開集合は25件です。確認した生成経路にはNuGet/npm package・container・Android成果物はありません。

## CIの動作

1. 共通actionは期待するファイル名の完全な集合を確認します。欠落・余分なファイル・ディレクトリ・symlinkは失敗にします。
2. 実アーカイブのバイトからSHA-256を計算し、個別checksumと照合します。統合manifestは初回だけ生成し、既存の不一致を上書きしません。
3. commit SHAで固定したactions/attest v4が、アーカイブ本体とchecksum・manifestへkeylessなSLSA provenanceを付与します。
4. gh attestation verifyが全subjectを暗号検証します。repo、release.yml、source SHA、source ref、workflow SHA、GitHub-hosted runnerを制約します。対象0件、CLI未導入、検証不能、失敗、90秒の応答期限超過は失敗にします。
5. native署名の停止ゲートがGitHub Release作成を止めます。署名のないWindows/macOS配布物をprovenanceだけで公開しません。

OIDC・attestationsの書込権限は関連ジョブだけに与えます。PRでは署名要求を実行せず、単体テストと既存のビルド・package・E2Eを実行します。タグの配布経路と、明示的に選んだ無害なsmoke fixtureだけが署名サービスを使います。

GitHubの[公式action](https://github.com/actions/attest)と[CLI検証仕様](https://cli.github.com/manual/gh_attestation_verify)に従います。公開repoでは公開Sigstoreが使われ、provenanceは公開されます。秘密や利用者データをsubjectへ含めないでください。

## 開発者の検証手順

リポジトリのルートで実行してください。Pythonは共通CA監査に合格した環境を使ってください。

```shell
python -m unittest discover -s tools/oop-design-checker/tests/release-provenance -p "test_*.py" -v
```

単体テストのgh呼出しはモックであり、暗号検証の成功証明ではありません。実際の署名と検証は専用smoke実行で確認してください。

```shell
gh workflow run release.yml --repo tomiya7688/oop-design-checker --ref <確認するbranch> -f provenance-smoke-only=true
```

この実行は「製品ビルドではない」と記したREADMEだけのfixtureを作ります。全subjectの実検証後、アーカイブとchecksumを両方改変した場合と、異なるsource SHAを要求した場合の拒否を確認します。元のfixtureへ復元し、bundleと拒否ログをprovenance-smoke-fixture artifactに保存します。製品のビルド・タグ作成・Release公開は行いません。

整形には[固定版formatterの手順](../../formatting/README.md)を使ってください。Pythonは変更したrelease-provenanceモジュール、Prettierは変更したYAMLと文書だけを指定してください。

## 利用者の検証手順

**以下は公開再開後の手順です。現時点で署名済み製品が公開されているという意味ではありません。**

1. 検証したい版の完全なcommit SHAを信頼できる経路から確認してください。
2. ダウンロードしたアーカイブとchecksum・manifestの各ファイルに次を実行してください。SHAとタグは確認した実在値へ置き換えてください。

```shell
gh attestation verify <対象ファイル> --repo tomiya7688/oop-design-checker --signer-workflow tomiya7688/oop-design-checker/.github/workflows/release.yml --source-ref refs/tags/v<版> --source-digest <40桁commitSHA> --signer-digest <40桁workflowSHA> --deny-self-hosted-runners
```

3. checksum照合とOS固有の署名・失効・notarization確認も実行してください。具体的なOS署名手順は導入後に追記します。検証失敗時は実行しないでください。

## 残る作業

- Windows用の信頼された署名証明書とRFC 3161 timestampの導入・検証。
- macOS用のDeveloper ID、notarization、Gatekeeper検証。
- 実署名が揃ってから、公開停止ステップを実検証ゲートへ置き換える変更のレビュー。
- 既存CI成果物・外部配布経路の調査。GitHub Release一覧0件でも、外部配布物の未存在は証明できません。
- #230のOS失効・交差署名経路・既存環境の確認。

provenanceはビルドの発行元を検証する手段です。コードの安全性・ライセンス・OSの署名状態・公開可否のすべてを証明するものではありません。
