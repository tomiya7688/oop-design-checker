using OopDesignChecker.Core;
using OopDesignChecker.Localization;

namespace OopDesignChecker.Application;

internal static class CommandLineOptionsParser
{
    public static string Usage(UserInterfaceLanguage language) =>
        UserInterfaceText.Select(
            language,
            "使い方: oop-design-checker [path] [--config file] [--fail-on danger|warning|attention] [--format text|json|sarif|github] [--output file] [--language ja|en] [--verbose] [--version]",
            "Usage: oop-design-checker [path] [--config file] [--fail-on danger|warning|attention] [--format text|json|sarif|github] [--output file] [--language ja|en] [--verbose] [--version]"
        );

    public static CommandLineParseResult Parse(IReadOnlyList<string> args)
    {
        var accumulator = new OptionAccumulator();
        for (var index = 0; index < args.Count; index++)
        {
            if (!accumulator.TryConsume(args, ref index, out var errorMessage))
            {
                return CommandLineParseResult.Failure(errorMessage!);
            }
        }

        return accumulator.BuildResult();
    }

    private sealed class OptionAccumulator
    {
        private string? _targetPath;
        private string? _configurationPath;
        private string? _outputPath;
        private DesignDiagnosticSeverity? _failureThreshold;
        private bool _verbose;
        private DiagnosticOutputFormat _outputFormat = DiagnosticOutputFormat.Text;
        private UserInterfaceLanguage _language = UserInterfaceText.DefaultLanguage;

        public bool TryConsume(IReadOnlyList<string> args, ref int index, out string? errorMessage)
        {
            var argument = args[index];
            return argument switch
            {
                "--config" => TrySetConfigurationPath(args, ref index, out errorMessage),
                "--fail-on" => TrySetFailureThreshold(args, ref index, out errorMessage),
                "--format" => TrySetOutputFormat(args, ref index, out errorMessage),
                "--output" => TrySetOutputPath(args, ref index, out errorMessage),
                "--language" => TrySetLanguage(args, ref index, out errorMessage),
                "--warnings-as-errors" => SetWarningsAsErrors(out errorMessage),
                "--verbose" => SetVerbose(out errorMessage),
                "--help" or "-h" => Fail(Usage(_language), out errorMessage),
                _ => TrySetTargetPath(argument, out errorMessage),
            };
        }

        public CommandLineParseResult BuildResult()
        {
            if (_outputFormat == DiagnosticOutputFormat.GitHub && _outputPath is not null)
            {
                return CommandLineParseResult.Failure(
                    Text(
                        "--format github ではworkflow annotationをstdoutへ出す必要があるため、--outputは使用できません。",
                        "--output cannot be used with --format github because workflow annotations must be written to stdout."
                    )
                );
            }

            _targetPath ??= Directory.GetCurrentDirectory();
            return CommandLineParseResult.Success(
                new CommandLineOptions(
                    Path.GetFullPath(_targetPath),
                    _configurationPath,
                    _failureThreshold,
                    _verbose,
                    _outputFormat,
                    _outputPath is null ? null : Path.GetFullPath(_outputPath),
                    _language
                )
            );
        }

        private bool TrySetConfigurationPath(
            IReadOnlyList<string> args,
            ref int index,
            out string? errorMessage
        )
        {
            if (!TryReadValue(args, ref index, out _configurationPath))
            {
                return Fail(
                    Text("--configには設定ファイルpathが必要です。", "--config requires a file path."),
                    out errorMessage
                );
            }

            return Succeed(out errorMessage);
        }

        private bool TrySetFailureThreshold(
            IReadOnlyList<string> args,
            ref int index,
            out string? errorMessage
        )
        {
            if (
                !TryReadValue(args, ref index, out var thresholdText)
                || !TryParseSeverity(thresholdText, out var parsedThreshold)
            )
            {
                return Fail(
                    Text(
                        "--fail-onにはdanger、warning、attentionのいずれかを指定してください。",
                        "--fail-on requires danger, warning, or attention."
                    ),
                    out errorMessage
                );
            }

            _failureThreshold = parsedThreshold;
            return Succeed(out errorMessage);
        }

        private bool TrySetOutputFormat(
            IReadOnlyList<string> args,
            ref int index,
            out string? errorMessage
        )
        {
            if (
                !TryReadValue(args, ref index, out var formatText)
                || !TryParseOutputFormat(formatText, out _outputFormat)
            )
            {
                return Fail(
                    Text(
                        "--formatにはtext、json、sarif、githubのいずれかを指定してください。",
                        "--format requires text, json, sarif, or github."
                    ),
                    out errorMessage
                );
            }

            return Succeed(out errorMessage);
        }

        private bool TrySetOutputPath(
            IReadOnlyList<string> args,
            ref int index,
            out string? errorMessage
        )
        {
            if (!TryReadValue(args, ref index, out _outputPath))
            {
                return Fail(
                    Text("--outputには出力ファイルpathが必要です。", "--output requires a file path."),
                    out errorMessage
                );
            }

            return Succeed(out errorMessage);
        }

        private bool TrySetLanguage(
            IReadOnlyList<string> args,
            ref int index,
            out string? errorMessage
        )
        {
            if (
                !TryReadValue(args, ref index, out var languageText)
                || !UserInterfaceText.TryParseLanguage(languageText, out _language)
            )
            {
                return Fail(
                    Text(
                        "--languageにはjaまたはenを指定してください。",
                        "--language requires ja or en."
                    ),
                    out errorMessage
                );
            }

            return Succeed(out errorMessage);
        }

        private bool SetWarningsAsErrors(out string? errorMessage)
        {
            _failureThreshold = DesignDiagnosticSeverity.Warning;
            return Succeed(out errorMessage);
        }

        private bool SetVerbose(out string? errorMessage)
        {
            _verbose = true;
            return Succeed(out errorMessage);
        }

        private bool TrySetTargetPath(string argument, out string? errorMessage)
        {
            if (argument.StartsWith('-'))
            {
                return Fail(
                    Text($"不明なoptionです: {argument}", $"Unknown option: {argument}"),
                    out errorMessage
                );
            }

            if (_targetPath is not null)
            {
                return Fail(
                    Text(
                        "解析対象pathは1つだけ指定できます。",
                        "Only one target path can be specified."
                    ),
                    out errorMessage
                );
            }

            _targetPath = argument;
            return Succeed(out errorMessage);
        }

        private static bool TryReadValue(
            IReadOnlyList<string> args,
            ref int index,
            out string? value
        )
        {
            if (index + 1 >= args.Count)
            {
                value = null;
                return false;
            }

            index++;
            value = args[index];
            return !string.IsNullOrWhiteSpace(value);
        }

        private static bool TryParseSeverity(string? value, out DesignDiagnosticSeverity severity)
        {
            if (Enum.TryParse(value, ignoreCase: true, out severity))
            {
                return true;
            }

            severity = default;
            return false;
        }

        private static bool TryParseOutputFormat(string? value, out DiagnosticOutputFormat format)
        {
            if (Enum.TryParse(value, ignoreCase: true, out format))
            {
                return true;
            }

            format = default;
            return false;
        }

        private string Text(string japanese, string english) =>
            UserInterfaceText.Select(_language, japanese, english);

        private static bool Succeed(out string? errorMessage)
        {
            errorMessage = null;
            return true;
        }

        private static bool Fail(string message, out string? errorMessage)
        {
            errorMessage = message;
            return false;
        }
    }
}
