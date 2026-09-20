namespace ReleaseValidation.Positive.Inheritance;

internal class HiddenBase
{
    public void Start() { }
    public int Read() => 1;
}

internal sealed class HiddenChild : HiddenBase
{
    public new void Start() { }
    public new int Read() => 2;
}

internal class WideBase
{
    public virtual void Start() => GC.KeepAlive(this);
    public virtual object? Read() => new object();
}

internal sealed class WideChild : WideBase
{
    public override void Start() { }
    public override object? Read() => null;
}

internal class ContractBase
{
    public virtual void Execute() { }
}

internal sealed class ContractBreaker : ContractBase
{
    public override void Execute() => throw new NotSupportedException();
}

internal class Depth1 { }
internal class Depth2 : Depth1 { }
internal class Depth3 : Depth2 { }
internal class Depth4 : Depth3 { }
internal class Depth5 : Depth4 { }

internal interface IClock
{
    int Read();
}

internal sealed class Clock : IClock
{
    public int Read() => 0;
}

internal sealed class ConcreteConsumer
{
    private readonly Clock _clock;
    public ConcreteConsumer(Clock clock) => _clock = clock;
    public int Run() => _clock.Read();
}

internal sealed class ConstructingConsumer
{
    private readonly IClock _clock = new Clock();
    public int Run() => _clock.Read();
}

internal class ReusableBase
{
    protected int Left;
    protected int Right;
}

internal sealed class Calculator : ReusableBase
{
    public int Sum() => Left + Right;
}
