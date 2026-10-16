namespace OopDesignChecker.Analysis;

internal static class 診断ファイルパス
{
    public const string 不明 = "<unknown>";

    public static string 正規化する(string? ファイルパス)
    {
        if (string.IsNullOrWhiteSpace(ファイルパス))
        {
            return 不明;
        }

        if (string.Equals(ファイルパス, 不明, StringComparison.Ordinal))
        {
            return ファイルパス;
        }

        try
        {
            return Path.GetFullPath(ファイルパス);
        }
        catch (Exception exception)
            when (exception is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return ファイルパス;
        }
    }

    public static bool ファイルURIを作成できる(string? ファイルパス, out string ファイルURI)
    {
        ファイルURI = string.Empty;
        if (
            string.IsNullOrWhiteSpace(ファイルパス)
            || string.Equals(ファイルパス, 不明, StringComparison.Ordinal)
        )
        {
            return false;
        }

        try
        {
            ファイルURI = new Uri(Path.GetFullPath(ファイルパス)).AbsoluteUri;
            return true;
        }
        catch (Exception exception)
            when (exception
                    is ArgumentException
                        or NotSupportedException
                        or PathTooLongException
                        or UriFormatException
            )
        {
            return false;
        }
    }
}
