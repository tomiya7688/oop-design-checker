namespace ReleaseValidation.Positive.VisibilityAndStatic;

public class HierarchyBase
{
    protected int Value;
}

public sealed class HierarchyLeaf : HierarchyBase
{
    public int Read() => Value;
}

internal class SealMe
{
    private int _count;
    public void Run() => _count++;
}

internal sealed class StaticMemberCandidate
{
    private int _count;
    public void Touch() => _count++;
    public int Constant() => 42;
}

internal class StaticClassCandidate
{
    public int Sum(int left, int right) => left + right;
    public int Difference(int left, int right) => left - right;
}

internal static class GlobalState
{
    private static int _count;
    public static void Increment() => _count++;
}
