namespace OopDesignChecker.Analysis;

internal static class パス比較規則
{
    public static StringComparer 文字列比較子 =>
        OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;

    public static StringComparison 文字列比較方式 =>
        OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
}
