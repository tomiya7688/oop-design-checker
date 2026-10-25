# 変更ファイルの整形

## 背景と目的

作業ごとの表記のばらつきを防ぎ、整形漏れをCIで検出します。C#は既存のCSharpierを使います。PythonはRuff 0.14.0、YAML・Markdown・JSONはPrettier 3.6.2を開発用に固定します。

## 準備

リポジトリのルートで、監査に合格したPythonとNode.jsを使って実行してください。

```shell
python -m pip install -r tools/formatting/requirements.txt
npm ci --prefix tools/formatting
```

## 実行方法

今回変更したファイルのパスを指定してください。次はPython監査モジュールの例です。

```shell
python -m ruff format --config tools/formatting/ruff.toml tools/oop-design-checker/tests/python-security
python -m ruff format --check --config tools/formatting/ruff.toml tools/oop-design-checker/tests/python-security
npm run --prefix tools/formatting format -- ../../.github/actions/setup-python-trust/action.yml
npm run --prefix tools/formatting format:check -- ../../.github/actions/setup-python-trust/action.yml
```

npmへ渡すパスはtools/formattingからの相対パスです。複数ファイルを列挙すれば1コマンドで整形・確認できます。設定はtools/formatting内に置き、担当外の未コミット変更・生成物・vendor・原文保持が必要なサンプルを一括整形しません。requirements.txtは依存指定の原文を保持し、対象外です。

既存C#の手順はtools/oop-design-checkerでdotnet tool restore後にdotnet csharpier format <変更ファイル>、dotnet csharpier check <変更ファイル>を実行します。Python/YAML/文書の既存ファイルは変更した範囲の作業単位から順に適用します。
