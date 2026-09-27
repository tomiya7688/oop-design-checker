namespace OopDesignChecker.Analysis;

internal static class DiagnosticPath
{
    public const string Unknown = "<unknown>";

    public static string Normalize(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return Unknown;
        }

        if (string.Equals(path, Unknown, StringComparison.Ordinal))
        {
            return path;
        }

        try
        {
            return Path.GetFullPath(path);
        }
        catch (Exception exception)
            when (exception is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return path;
        }
    }

    public static bool TryCreateFileUri(string? path, out string uri)
    {
        uri = string.Empty;
        if (
            string.IsNullOrWhiteSpace(path)
            || string.Equals(path, Unknown, StringComparison.Ordinal)
        )
        {
            return false;
        }

        try
        {
            uri = new Uri(Path.GetFullPath(path)).AbsoluteUri;
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
