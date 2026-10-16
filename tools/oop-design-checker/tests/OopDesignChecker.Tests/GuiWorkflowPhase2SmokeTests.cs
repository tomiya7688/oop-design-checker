using System.Text.Json;
using OopDesignChecker.Configuration;
using OopDesignChecker.Core;
using OopDesignChecker.Gui;
using OopDesignChecker.Output;

namespace OopDesignChecker.Tests;

internal static class GUIワークフロー第2段階スモークテスト
{
    public static void 実行する()
    {
        設定JSONを往復変換して検証する();
        診断出力サービスがJSONとSARIFを書き出す();
        チェッカーが事前キャンセルを反映する();
        クリップボード失敗を捕捉する();
        UI自動化設定は明示時のみ有効になる();
    }

    private static void 設定JSONを往復変換して検証する()
    {
        var 設定 = new CheckerConfiguration
        {
            IgnoredPaths = ["**/Generated/**"],
            DisabledRules = ["OOP103"],
            FailureThreshold = DesignDiagnosticSeverity.Warning,
            RuleSettings = new CheckerRuleSettings
            {
                Oop304 = new InheritanceDepthRuleSettings { WarningDepth = 6 },
            },
        };

        var JSON文字列 = CheckerConfigurationJson.Serialize(設定);
        var 解析済み = CheckerConfigurationJson.Parse(JSON文字列);
        if (
            解析済み.FailureThreshold != DesignDiagnosticSeverity.Warning
            || 解析済み.IgnoredPaths.Count != 1
            || 解析済み.DisabledRules.Count != 1
            || 解析済み.RuleSettings.Oop304.WarningDepth != 6
        )
        {
            throw new InvalidOperationException(
                "Shared checker configuration JSON did not round-trip correctly."
            );
        }

        try
        {
            _ = CheckerConfigurationJson.Parse(
                """
                {
                  "ruleSettings": {
                    "oop304": {
                      "warningDepth": 0
                    }
                  }
                }
                """
            );
        }
        catch (InvalidOperationException 例外)
            when (例外.Message.Contains("warningDepth", StringComparison.Ordinal))
        {
            return;
        }

        throw new InvalidOperationException(
            "Invalid OOP304 configuration was accepted by the shared JSON validator."
        );
    }

    private static void 診断出力サービスがJSONとSARIFを書き出す()
    {
        var 一時ディレクトリ = Path.Combine(
            Path.GetTempPath(),
            $"oop-checker-gui-export-{Guid.NewGuid():N}"
        );
        Directory.CreateDirectory(一時ディレクトリ);

        try
        {
            var 診断一覧 = new[]
            {
                new DesignDiagnostic(
                    new RuleDescriptor(
                        "OOP999",
                        "Smoke diagnostic",
                        DesignDiagnosticSeverity.Warning
                    ),
                    DesignDiagnosticSeverity.Warning,
                    "Export smoke diagnostic.",
                    "Sample.実行する",
                    new SourceLocation(Path.Combine(一時ディレクトリ, "Sample.cs"), 3, 5)
                ),
            };
            var JSONパス = Path.Combine(一時ディレクトリ, "result.json");
            var SARIFパス = Path.Combine(一時ディレクトリ, "result.sarif");

            DiagnosticExportService.Export(診断一覧, JSONパス, DiagnosticExportFormat.Json);
            DiagnosticExportService.Export(診断一覧, SARIFパス, DiagnosticExportFormat.Sarif);

            using var JSON文書 = JsonDocument.Parse(File.ReadAllText(JSONパス));
            using var SARIF文書 = JsonDocument.Parse(File.ReadAllText(SARIFパス));
            var JSON規則 = JSON文書
                .RootElement.GetProperty("diagnostics")[0]
                .GetProperty("ruleId")
                .GetString();
            var SARIFバージョン = SARIF文書.RootElement.GetProperty("version").GetString();
            if (JSON規則 != "OOP999" || SARIFバージョン != "2.1.0")
            {
                throw new InvalidOperationException(
                    "Shared diagnostic export did not produce the expected JSON/SARIF documents."
                );
            }
        }
        finally
        {
            Directory.Delete(一時ディレクトリ, recursive: true);
        }
    }

    private static void UI自動化設定は明示時のみ有効になる()
    {
        const string 遅延変数名 = "OOP_DESIGN_CHECKER_UI_AUTOMATION_ANALYSIS_DELAY_MS";
        const string 出力先変数名 = "OOP_DESIGN_CHECKER_UI_AUTOMATION_EXPORT_DIR";
        var 変更前の遅延 = Environment.GetEnvironmentVariable(遅延変数名);
        var 変更前の出力先 = Environment.GetEnvironmentVariable(出力先変数名);
        var 一時ディレクトリ = Path.Combine(
            Path.GetTempPath(),
            $"oop-checker-ui-automation-{Guid.NewGuid():N}"
        );

        try
        {
            Environment.SetEnvironmentVariable(遅延変数名, null);
            Environment.SetEnvironmentVariable(出力先変数名, null);
            if (
                UI自動化設定.解析遅延ミリ秒 != 0
                || UI自動化設定.出力先パス(DiagnosticExportFormat.Json) is not null
            )
            {
                throw new InvalidOperationException(
                    "GUI automation settings changed normal runtime behavior without opt-in."
                );
            }

            Environment.SetEnvironmentVariable(遅延変数名, "2500");
            Environment.SetEnvironmentVariable(出力先変数名, 一時ディレクトリ);
            var JSONパス = UI自動化設定.出力先パス(DiagnosticExportFormat.Json);
            var SARIFパス = UI自動化設定.出力先パス(DiagnosticExportFormat.Sarif);
            if (
                UI自動化設定.解析遅延ミリ秒 != 2500
                || JSONパス != Path.Combine(一時ディレクトリ, "actual-diagnostics.json")
                || SARIFパス != Path.Combine(一時ディレクトリ, "actual-diagnostics.sarif")
            )
            {
                throw new InvalidOperationException(
                    "GUI automation settings did not resolve deterministic test values."
                );
            }

            Environment.SetEnvironmentVariable(遅延変数名, "30001");
            if (UI自動化設定.解析遅延ミリ秒 != 0)
            {
                throw new InvalidOperationException(
                    "GUI automation delay accepted an out-of-range value."
                );
            }
        }
        finally
        {
            Environment.SetEnvironmentVariable(遅延変数名, 変更前の遅延);
            Environment.SetEnvironmentVariable(出力先変数名, 変更前の出力先);
        }
    }

    private static void クリップボード失敗を捕捉する()
    {
        var コピー済み = クリップボード操作
            .テキスト設定を試す非同期(() =>
                Task.FromException(new InvalidOperationException("clipboard failed"))
            )
            .GetAwaiter()
            .GetResult();

        if (コピー済み)
        {
            throw new InvalidOperationException(
                "ClipboardOperation reported success when the clipboard operation failed."
            );
        }
    }

    private static void チェッカーが事前キャンセルを反映する()
    {
        using var キャンセル = new CancellationTokenSource();
        キャンセル.Cancel();

        try
        {
            _ = CheckerService.Analyze(
                Directory.GetCurrentDirectory(),
                cancellationToken: キャンセル.Token
            );
        }
        catch (OperationCanceledException)
        {
            return;
        }

        throw new InvalidOperationException(
            "CheckerService ignored an already-cancelled analysis request."
        );
    }
}
