using OopDesignChecker.Core;
using OopDesignChecker.Localization;

namespace OopDesignChecker.Application;

internal static class CommandLineOptionsParser
{
    public static CommandLineParseResult Parse(IReadOnlyList<string> args)
    {
        var language = ResolveRequestedLanguage(args);
        var accumulator = new OptionAccumulator(language);
        for (var index = 0; index < args.Count; index++)
        {
            if (!accumulator.TryConsume(args, ref index, out var errorMessage))
            {
                return CommandLineParseResult.Failure(errorMessage!);
            }
        }

        return accumulator.BuildResult();
    }

    public static string Usage(UserInterfaceLanguage language) => LocalizedText.Usage(language);

    public static UserInterfaceLanguage ResolveRequestedLanguage(IReadOnlyList<string> args)
    {
        for (var index = 0; index + 1 < args.Count; index++)
        {
            if (
                args[index] == "--language"
                && LocalizedText.TryParseLanguage(args[index + 1], out var language)
            )
            {
                return language;
            }
        }

        return LocalizedText.DefaultLanguage;
    }

    private sealed class OptionAccumulator
    {
        private string? _targetPath;
        private string? _configurationPath;
        private string? _outputPath;
        private DesignDiagnosticSeverity? _failureThreshold;
        private bool _verbose;
        private DiagnosticOutputFormat _outputFormat = DiagnosticOutputFormat.Text;
        private UserInterfaceLanguage _language;

        public OptionAccumulator(UserInterfaceLanguage language)
        {
            _language = language;
        }

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
                    LocalizedText.GitHubOutputCannotUseFile(_language)
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
                return Fail(LocalizedText.ConfigRequiresPath(_language), out errorMessage);
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
                return Fail(LocalizedText.FailOnRequiresSeverity(_language), out errorMessage);
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
                return Fail(LocalizedText.FormatRequiresValue(_language), out errorMessage);
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
                return Fail(LocalizedText.OutputRequiresPath(_language), out errorMessage);
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
                || !LocalizedText.TryParseLanguage(languageText, out _language)
            )
            {
                return Fail(LocalizedText.LanguageRequiresValue(_language), out errorMessage);
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
                return Fail(LocalizedText.UnknownOption(_language, argument), out errorMessage);
            }

            if (_targetPath is not null)
            {
                return Fail(LocalizedText.OnlyOneTarget(_language), out errorMessage);
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
            if (
                Enum.TryParse(value, ignoreCase: true, out severity)
                && Enum.IsDefined(severity)
                && !int.TryParse(value, out _)
            )
            {
                return true;
            }

            severity = default;
            return false;
        }

        private static bool TryParseOutputFormat(string? value, out DiagnosticOutputFormat format)
        {
            if (
                Enum.TryParse(value, ignoreCase: true, out format)
                && Enum.IsDefined(format)
                && !int.TryParse(value, out _)
            )
            {
                return true;
            }

            format = default;
            return false;
        }

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
