namespace ReleaseValidation.Boundary.Static;

internal class SampleException : Exception
{
    private int _count;

    public void Touch() => _count++;

    public string Describe() => "sample";
}

internal static class PerThreadState
{
    [ThreadStatic]
    private static int _count;

    public static void Increment() => _count++;
}
