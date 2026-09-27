namespace OopDesignChecker.Output;

internal sealed class DiagnosticOutputTarget
{
    private readonly string? _outputPath;

    public DiagnosticOutputTarget(string? outputPath)
    {
        _outputPath = outputPath;
    }

    public void Write(Action<TextWriter> action)
    {
        if (_outputPath is null)
        {
            action(Console.Out);
            return;
        }

        var directory = Path.GetDirectoryName(_outputPath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        using var writer = new StreamWriter(_outputPath, append: false);
        action(writer);
    }
}
