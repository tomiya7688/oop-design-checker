using OopDesignChecker.Core;
using OopDesignChecker.Localization;

namespace OopDesignChecker.Application;

internal static class コマンドライン引数解析器
{
    public static コマンドライン解析結果 Parse(IReadOnlyList<string> args)
    {
        var 言語 = ResolveRequestedLanguage(args);
        var 引数蓄積器 = new コマンドライン引数蓄積器(言語);
        for (var 位置 = 0; 位置 < args.Count; 位置++)
        {
            if (!引数蓄積器.TryConsume(args, ref 位置, out var エラー内容))
            {
                return コマンドライン解析結果.Failure(エラー内容!);
            }
        }

        return 引数蓄積器.BuildResult();
    }

    public static string Usage(UserInterfaceLanguage 言語) => LocalizedText.Usage(言語);

    public static UserInterfaceLanguage ResolveRequestedLanguage(IReadOnlyList<string> args)
    {
        for (var 位置 = 0; 位置 + 1 < args.Count; 位置++)
        {
            if (
                args[位置] == "--language"
                && LocalizedText.TryParseLanguage(args[位置 + 1], out var 言語)
            )
            {
                return 言語;
            }
        }

        return LocalizedText.DefaultLanguage;
    }

    private sealed class コマンドライン引数蓄積器
    {
        private string? _対象パス;
        private string? _設定パス;
        private string? _出力パス;
        private DesignDiagnosticSeverity? _失敗基準;
        private bool _詳細表示;
        private 診断出力形式 _出力形式 = 診断出力形式.Text;
        private UserInterfaceLanguage _表示言語;

        public コマンドライン引数蓄積器(UserInterfaceLanguage 言語)
        {
            _表示言語 = 言語;
        }

        public bool TryConsume(IReadOnlyList<string> args, ref int 位置, out string? エラー内容)
        {
            var 引数 = args[位置];
            return 引数 switch
            {
                "--config" => 設定パスを設定する(args, ref 位置, out エラー内容),
                "--fail-on" => 失敗基準を設定する(args, ref 位置, out エラー内容),
                "--format" => 出力形式を設定する(args, ref 位置, out エラー内容),
                "--output" => 出力先を設定する(args, ref 位置, out エラー内容),
                "--language" => 表示言語を設定する(args, ref 位置, out エラー内容),
                "--warnings-as-errors" => 警告を失敗基準にする(out エラー内容),
                "--verbose" => 詳細表示を有効にする(out エラー内容),
                "--help" or "-h" => 失敗を返す(Usage(_表示言語), out エラー内容),
                _ => 対象パスを設定する(引数, out エラー内容),
            };
        }

        public コマンドライン解析結果 BuildResult()
        {
            if (_出力形式 == 診断出力形式.GitHub && _出力パス is not null)
            {
                return コマンドライン解析結果.Failure(
                    LocalizedText.GitHubOutputCannotUseFile(_表示言語)
                );
            }

            _対象パス ??= Directory.GetCurrentDirectory();
            return コマンドライン解析結果.Success(
                new コマンドライン引数(
                    Path.GetFullPath(_対象パス),
                    _設定パス,
                    _失敗基準,
                    _詳細表示,
                    _出力形式,
                    _出力パス is null ? null : Path.GetFullPath(_出力パス),
                    _表示言語
                )
            );
        }

        private bool 設定パスを設定する(
            IReadOnlyList<string> args,
            ref int 位置,
            out string? エラー内容
        )
        {
            if (!次の値を読む(args, ref 位置, out _設定パス))
            {
                return 失敗を返す(LocalizedText.ConfigRequiresPath(_表示言語), out エラー内容);
            }

            return 成功を返す(out エラー内容);
        }

        private bool 失敗基準を設定する(
            IReadOnlyList<string> args,
            ref int 位置,
            out string? エラー内容
        )
        {
            if (
                !次の値を読む(args, ref 位置, out var 失敗基準文字列)
                || !重要度を解析する(失敗基準文字列, out var 解析済み失敗基準)
            )
            {
                return 失敗を返す(LocalizedText.FailOnRequiresSeverity(_表示言語), out エラー内容);
            }

            _失敗基準 = 解析済み失敗基準;
            return 成功を返す(out エラー内容);
        }

        private bool 出力形式を設定する(
            IReadOnlyList<string> args,
            ref int 位置,
            out string? エラー内容
        )
        {
            if (
                !次の値を読む(args, ref 位置, out var 出力形式文字列)
                || !出力形式を解析する(出力形式文字列, out _出力形式)
            )
            {
                return 失敗を返す(LocalizedText.FormatRequiresValue(_表示言語), out エラー内容);
            }

            return 成功を返す(out エラー内容);
        }

        private bool 出力先を設定する(
            IReadOnlyList<string> args,
            ref int 位置,
            out string? エラー内容
        )
        {
            if (!次の値を読む(args, ref 位置, out _出力パス))
            {
                return 失敗を返す(LocalizedText.OutputRequiresPath(_表示言語), out エラー内容);
            }

            return 成功を返す(out エラー内容);
        }

        private bool 表示言語を設定する(
            IReadOnlyList<string> args,
            ref int 位置,
            out string? エラー内容
        )
        {
            if (
                !次の値を読む(args, ref 位置, out var 表示言語文字列)
                || !LocalizedText.TryParseLanguage(表示言語文字列, out _表示言語)
            )
            {
                return 失敗を返す(LocalizedText.LanguageRequiresValue(_表示言語), out エラー内容);
            }

            return 成功を返す(out エラー内容);
        }

        private bool 警告を失敗基準にする(out string? エラー内容)
        {
            _失敗基準 = DesignDiagnosticSeverity.Warning;
            return 成功を返す(out エラー内容);
        }

        private bool 詳細表示を有効にする(out string? エラー内容)
        {
            _詳細表示 = true;
            return 成功を返す(out エラー内容);
        }

        private bool 対象パスを設定する(string 引数, out string? エラー内容)
        {
            if (引数.StartsWith('-'))
            {
                return 失敗を返す(LocalizedText.UnknownOption(_表示言語, 引数), out エラー内容);
            }

            if (_対象パス is not null)
            {
                return 失敗を返す(LocalizedText.OnlyOneTarget(_表示言語), out エラー内容);
            }

            _対象パス = 引数;
            return 成功を返す(out エラー内容);
        }

        private static bool 次の値を読む(IReadOnlyList<string> args, ref int 位置, out string? 値)
        {
            if (位置 + 1 >= args.Count)
            {
                値 = null;
                return false;
            }

            位置++;
            値 = args[位置];
            return !string.IsNullOrWhiteSpace(値);
        }

        private static bool 重要度を解析する(string? 値, out DesignDiagnosticSeverity 重要度)
        {
            if (
                Enum.TryParse(値, ignoreCase: true, out 重要度)
                && Enum.IsDefined(重要度)
                && !int.TryParse(値, out _)
            )
            {
                return true;
            }

            重要度 = default;
            return false;
        }

        private static bool 出力形式を解析する(string? 値, out 診断出力形式 形式)
        {
            if (
                Enum.TryParse(値, ignoreCase: true, out 形式)
                && Enum.IsDefined(形式)
                && !int.TryParse(値, out _)
            )
            {
                return true;
            }

            形式 = default;
            return false;
        }

        private static bool 成功を返す(out string? エラー内容)
        {
            エラー内容 = null;
            return true;
        }

        private static bool 失敗を返す(string message, out string? エラー内容)
        {
            エラー内容 = message;
            return false;
        }
    }
}
