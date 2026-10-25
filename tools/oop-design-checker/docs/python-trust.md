# Python環境の更新とCA点検

## 背景と目的

Python周辺のeMudhra / emSign証明書対応として、CIが古いpipやcertifiを使い続けないようにします。#230のうち、CIの更新と実CA集合の静的検査を実装します。

根拠は[Python公式告知](https://discuss.python.org/t/information-for-python-users-regarding-emudhra-emsign-ca-certificate-security-incident/109576)と[certifiの勧告](https://github.com/certifi/python-certifi/security/advisories/GHSA-6q48-6c7w-9gw2)です。最低版はpip 26.2.2、certifi 2026.10.19、利用時のuv 0.13.4・sigstore 4.5.1です。

## CIの動作

共通actionはPython 3.13.17を選び、公式告知のSHA-256付きURLからpipを更新し、certifiを更新し、ジョブ内のPython/OpenSSLとrequestsのCA参照をその実ファイルへ設定してから監査します。SSL_CERT_DIRはこのジョブでは空にし、旧CAディレクトリの遅延読込を避けます。Windowsの既定SSL contextは環境変数のCAに加えてWindows証明書ストアも読み込みます。このため、使い捨てのGitHub Windows runnerではCurrentUser/LocalMachineのRoot・CAストアを読み、G3のDER SHA-256が完全一致する証明書だけを除去してから監査します。除去・再監査に失敗したジョブは停止します。利用者のPCでこの除去手順を自動実行しません。証明書検証を無効にするオプションは使いません。Appium・AI判定用requirementsにもcertifiの最低版を指定します。

監査は実行中Pythonのパス・版、パッケージ版、certifiとpip同梱certifiの実ファイル・ファイルSHA-256、既定SSL contextのCA数、G3ルートのDER SHA-256一致、CA上書きを記録します。空のCA、読取失敗、古い必須版、pipのtrusted-host、明示SSL_CERT_DIRは失敗にします。pip設定の認証URLやAPIキーは出力しません。

CI、release、release-candidate、ui-appium、ui-ai-contract、ui-ai-panelのPython利用ジョブへ適用します。専用のpython-securityジョブは3 OSで単体テストと監査を実行し、JSONをartifactへ保存します。依存をインストールするAppium・AIジョブではインストール後にも監査します。

## 利用者の実行手順

既存venvごとに、そのvenvのPythonで次を実行してください。新しい環境へ更新しても既存venvは自動更新されません。リポジトリのルートで実行します。

1. 公式のハッシュ付きpipをインストールしてください。

```shell
python -m pip install --no-deps --upgrade "pip @ https://files.pythonhosted.org/packages/3f/06/3e4420302363fbb71be3c7f35ce042f54bfca330e3e66b3a2e3c4cca579f/pip-26.2.2-py3-none-any.whl#sha256=fd7f4dfa8190e014013b9f08f3e973f4e4c26ffd179a204895533a7254813a70"
python -m pip install --upgrade "certifi>=2026.10.19"
```

2. 必要なrequirementsをインストール後、実環境を点検してください。

```shell
python tools/oop-design-checker/tests/python-security/audit_python_trust.py
python -m unittest discover -s tools/oop-design-checker/tests/python-security -p "test_*.py" -v
```

3. JSONのpython、packages、trust_sources、errorsを確認してください。終了コード0は、この静的検査に合格したことだけを表します。CAパスには利用者名などが含まれるため、共有時は非公開情報を除いてください。

## 検証範囲と未完了事項

このスクリプトはOSの信頼設定を変更しません。取得済みCA集合とG3の不在を点検し、失効・交差署名の全経路を検証するものではありません。既定CAディレクトリは遅延読込される場合があり、Windows/macOSのネイティブ失効判定とも同一ではありません。

[Appleの説明](https://support.apple.com/en-us/149555)にあるAAA Certificate Services経由の交差署名も区別する必要があります。G3が存在しないことだけを理由に、問題チェーン全体が拒否されるとは報告しないでください。#230は、既存環境の全信頼経路・uv/sigstoreの実使用元・既存成果物の確認が終わるまで完了にしません。

このリポジトリの配布対象は.NETのCUI/GUIで、Pythonはテスト・判定・パッケージ検査に使用します。パッケージ未導入という結果は、別のPATH上のCLIや他プロジェクトの未使用証明にはなりません。#231の署名・attestation対応も別途必要です。
