namespace OopDesignChecker.Output;

internal sealed class 診断出力先
{
    private readonly string? _出力パス;

    public 診断出力先(string? outputPath)
    {
        _出力パス = outputPath;
    }

    public void Write(Action<TextWriter> action)
    {
        if (_出力パス is null)
        {
            action(Console.Out);
            return;
        }

        var ディレクトリ = Path.GetDirectoryName(_出力パス);
        if (!string.IsNullOrWhiteSpace(ディレクトリ))
        {
            Directory.CreateDirectory(ディレクトリ);
        }

        using var 書き込み器 = new StreamWriter(_出力パス, append: false);
        action(書き込み器);
    }
}
