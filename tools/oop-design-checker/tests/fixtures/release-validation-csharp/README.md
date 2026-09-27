# C# release validation fixture

OOP Design Checker 1.0.0 のrelease前検証で、CI・AI UI分析・手動GUI確認のすべてから共通利用する固定fixtureです。

- `Positive/`: 現行24 ruleを少なくとも1回ずつ意図的に発火させます。
- `Boundary/`: 代表的な正当ケース・境界ケースです。対応ruleを誤検知してはいけません。
- `expected-diagnostics.json`: positive scenarioとboundary禁止条件のmachine-readable contractです。

実行例:

```bash
dotnet run --project src/frontends/cui -- tests/fixtures/release-validation-csharp --format json --output release-validation.json
```

このfixtureはrelease時にもそのまま使用します。CI専用に生成し直さず、内容を変更する場合は期待値も同じPRで更新します。
