using OopDesignChecker;
using OopDesignChecker.Application;
using OopDesignChecker.Core;
using OopDesignChecker.Localization;
using Xunit;

namespace OopDesignChecker.ContractTests;

public sealed class CommandLineContractTests
{
    [Fact]
    public void TargetDefaultsToCurrentDirectory()
    {
        var result = CommandLineOptionsParser.Parse([]);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Options);
        Assert.Equal(
            Path.GetFullPath(Directory.GetCurrentDirectory()),
            result.Options.TargetPath
        );
    }

    [Theory]
    [InlineData("sample.cs")]
    [InlineData("sample.csproj")]
    [InlineData("sample.sln")]
    [InlineData("sample.slnx")]
    [InlineData("sample-folder")]
    public void SupportedTargetShapesReachTheApplication(string target)
    {
        var result = CommandLineOptionsParser.Parse([target]);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Options);
        Assert.Equal(Path.GetFullPath(target), result.Options.TargetPath);
    }

    [Theory]
    [InlineData("--config")]
    [InlineData("--fail-on")]
    [InlineData("--format")]
    [InlineData("--output")]
    [InlineData("--language")]
    public void OptionsThatRequireValuesRejectMissingValues(string option)
    {
        var result = CommandLineOptionsParser.Parse([option]);

        Assert.False(result.IsSuccess);
        Assert.Null(result.Options);
        Assert.False(string.IsNullOrWhiteSpace(result.ErrorMessage));
    }

    [Fact]
    public void UnknownOptionIsRejected()
    {
        var result = CommandLineOptionsParser.Parse(["--not-a-real-option"]);

        Assert.False(result.IsSuccess);
        Assert.Contains("--not-a-real-option", result.ErrorMessage, StringComparison.Ordinal);
    }

    [Fact]
    public void MultipleTargetsAreRejected()
    {
        var result = CommandLineOptionsParser.Parse(["one.cs", "two.cs"]);

        Assert.False(result.IsSuccess);
        Assert.Null(result.Options);
    }

    [Theory]
    [InlineData("attention", DesignDiagnosticSeverity.Attention)]
    [InlineData("warning", DesignDiagnosticSeverity.Warning)]
    [InlineData("danger", DesignDiagnosticSeverity.Danger)]
    public void FailureThresholdAcceptsEveryNamedSeverity(
        string value,
        DesignDiagnosticSeverity expected
    )
    {
        var result = CommandLineOptionsParser.Parse(["--fail-on", value]);

        Assert.True(result.IsSuccess);
        Assert.Equal(expected, result.Options!.FailureThreshold);
    }

    [Theory]
    [InlineData("999")]
    [InlineData("critical")]
    public void FailureThresholdRejectsInvalidValues(string value)
    {
        var result = CommandLineOptionsParser.Parse(["--fail-on", value]);

        Assert.False(result.IsSuccess);
    }

    [Theory]
    [InlineData("text", DiagnosticOutputFormat.Text)]
    [InlineData("json", DiagnosticOutputFormat.Json)]
    [InlineData("sarif", DiagnosticOutputFormat.Sarif)]
    [InlineData("github", DiagnosticOutputFormat.GitHub)]
    public void OutputFormatAcceptsEveryNamedValue(
        string value,
        DiagnosticOutputFormat expected
    )
    {
        var result = CommandLineOptionsParser.Parse(["--format", value]);

        Assert.True(result.IsSuccess);
        Assert.Equal(expected, result.Options!.OutputFormat);
    }

    [Theory]
    [InlineData("999")]
    [InlineData("xml")]
    public void OutputFormatRejectsInvalidValues(string value)
    {
        var result = CommandLineOptionsParser.Parse(["--format", value]);

        Assert.False(result.IsSuccess);
    }

    [Theory]
    [InlineData("ja", UserInterfaceLanguage.Japanese)]
    [InlineData("en", UserInterfaceLanguage.English)]
    public void LanguageAcceptsSupportedValues(string value, UserInterfaceLanguage expected)
    {
        var result = CommandLineOptionsParser.Parse(["--language", value]);

        Assert.True(result.IsSuccess);
        Assert.Equal(expected, result.Options!.Language);
    }

    [Fact]
    public void LanguageRejectsUnknownValue()
    {
        var result = CommandLineOptionsParser.Parse(["--language", "fr"]);

        Assert.False(result.IsSuccess);
    }

    [Fact]
    public void WarningsAsErrorsMapsToWarningThreshold()
    {
        var result = CommandLineOptionsParser.Parse(["--warnings-as-errors"]);

        Assert.True(result.IsSuccess);
        Assert.Equal(DesignDiagnosticSeverity.Warning, result.Options!.FailureThreshold);
    }

    [Fact]
    public void VerboseFlagIsPreserved()
    {
        var result = CommandLineOptionsParser.Parse(["--verbose"]);

        Assert.True(result.IsSuccess);
        Assert.True(result.Options!.Verbose);
    }

    [Fact]
    public void OutputPathIsNormalized()
    {
        var result = CommandLineOptionsParser.Parse([
            "--format",
            "json",
            "--output",
            "results.json",
        ]);

        Assert.True(result.IsSuccess);
        Assert.Equal(Path.GetFullPath("results.json"), result.Options!.OutputPath);
    }

    [Fact]
    public void GithubAnnotationsRejectOutputFile()
    {
        var result = CommandLineOptionsParser.Parse([
            "--format",
            "github",
            "--output",
            "annotations.txt",
        ]);

        Assert.False(result.IsSuccess);
    }

    [Fact]
    public void VersionAliasesReturnProductVersion()
    {
        AssertVersion("--version");
        AssertVersion("-V");
    }

    [Fact]
    public void HelpReturnsControlledUsageExit()
    {
        var result = RunCommand(["--help"]);

        Assert.Equal(2, result.ExitCode);
        Assert.Contains(
            CommandLineOptionsParser.Usage(UserInterfaceLanguage.Japanese),
            result.Error,
            StringComparison.Ordinal
        );
    }

    [Fact]
    public void CliExitCodesCoverSuccessDiagnosticFailureAndUsageFailure()
    {
        WithTemporaryDirectory(root =>
        {
            var clean = Path.Combine(root, "Clean.cs");
            var danger = Path.Combine(root, "Danger.cs");
            File.WriteAllText(
                clean,
                "internal sealed class Clean { private int _value; public int Value => _value; }"
            );
            File.WriteAllText(danger, "internal sealed class Danger { public int Value; }");

            Assert.Equal(0, RunCommand([clean, "--fail-on", "danger"]).ExitCode);
            Assert.Equal(1, RunCommand([danger, "--fail-on", "danger"]).ExitCode);
            Assert.Equal(
                2,
                RunCommand([Path.Combine(root, "missing-target")]).ExitCode
            );
        });
    }

    [Fact]
    public void JsonOutputCreatesRequestedFile()
    {
        WithTemporaryDirectory(root =>
        {
            var source = Path.Combine(root, "Danger.cs");
            var output = Path.Combine(root, "diagnostics.json");
            File.WriteAllText(source, "internal sealed class Danger { public int Value; }");

            var result = RunCommand([
                source,
                "--format",
                "json",
                "--output",
                output,
                "--fail-on",
                "danger",
            ]);

            Assert.Equal(1, result.ExitCode);
            Assert.True(File.Exists(output));
            Assert.Contains(
                @"""ruleId"": ""OOP106""",
                File.ReadAllText(output),
                StringComparison.Ordinal
            );
        });
    }

    [Fact]
    public void OutputWriteFailureReturnsControlledExitCode()
    {
        WithTemporaryDirectory(root =>
        {
            var source = Path.Combine(root, "Clean.cs");
            var output = Path.Combine(root, "missing-directory", "diagnostics.json");
            File.WriteAllText(
                source,
                "internal sealed class Clean { private int _value; public int Value => _value; }"
            );

            var result = RunCommand([source, "--format", "json", "--output", output]);

            Assert.Equal(2, result.ExitCode);
            Assert.False(string.IsNullOrWhiteSpace(result.Error));
        });
    }

    private static void AssertVersion(string option)
    {
        var result = RunCommand([option]);

        Assert.Equal(0, result.ExitCode);
        Assert.Equal("1.0.0", result.Output.Trim());
        Assert.True(string.IsNullOrWhiteSpace(result.Error));
    }

    private static CommandResult RunCommand(IReadOnlyList<string> args)
    {
        var originalOut = Console.Out;
        var originalError = Console.Error;
        using var output = new StringWriter();
        using var error = new StringWriter();
        try
        {
            Console.SetOut(output);
            Console.SetError(error);
            var exitCode = CheckerCommandLine.Run(args);
            return new CommandResult(exitCode, output.ToString(), error.ToString());
        }
        finally
        {
            Console.SetOut(originalOut);
            Console.SetError(originalError);
        }
    }

    private static void WithTemporaryDirectory(Action<string> action)
    {
        var root = Path.Combine(
            Path.GetTempPath(),
            "oop-design-checker-contract-tests",
            Guid.NewGuid().ToString("N")
        );
        Directory.CreateDirectory(root);
        try
        {
            action(root);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    private sealed record CommandResult(int ExitCode, string Output, string Error);
}
