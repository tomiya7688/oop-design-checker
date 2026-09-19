namespace OopDesignChecker.Analysis;

internal static class DiagnosticPath
{
    public static string NormalizeForIdentity(string path)
    {
        if (!LooksLikeFileSystemPath(path))
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

    public static string ToSarifUri(string path)
    {
        var normalized = NormalizeForIdentity(path);
        if (!LooksLikeFileSystemPath(normalized))
        {
            return normalized;
        }

        try
        {
            return new Uri(Path.GetFullPath(normalized)).AbsoluteUri;
        }
        catch (Exception exception)
            when (exception is ArgumentException or UriFormatException or NotSupportedException or PathTooLongException)
        {
            return normalized;
        }
    }

    private static bool LooksLikeFileSystemPath(string path) =>
        !string.IsNullOrWhiteSpace(path)
        && !(path.StartsWith('<') && path.EndsWith('>'));
}
