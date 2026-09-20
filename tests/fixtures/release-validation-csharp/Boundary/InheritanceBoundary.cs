namespace ReleaseValidation.Boundary.Inheritance;

internal abstract class ValidBase
{
    public abstract int Run();
}

internal sealed class ValidChild : ValidBase
{
    public override int Run() => 1;
}

internal class Shallow1 { }

internal class Shallow2 : Shallow1 { }

internal class Shallow3 : Shallow2 { }

internal interface IService
{
    int Run();
}

internal sealed class SpecializedService : IService
{
    public int Run() => 1;

    public int ConcreteOnly() => 2;
}

internal sealed class SpecializedConsumer
{
    private readonly SpecializedService _service;

    public SpecializedConsumer(SpecializedService service) => _service = service;

    public int Run() => _service.Run() + _service.ConcreteOnly();
}

internal readonly record struct Money(int Amount);

internal sealed class MoneyOwner
{
    private readonly Money _money = new(10);

    public int Read() => _money.Amount;
}

internal class ProcessorBase
{
    protected int ReadLeft() => 1;

    protected int ReadRight() => 2;

    public virtual int Run() => 0;
}

internal sealed class SpecializedProcessor : ProcessorBase
{
    public override int Run() => ReadLeft() + ReadRight();
}
