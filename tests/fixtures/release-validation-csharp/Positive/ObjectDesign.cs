namespace ReleaseValidation.Positive.ObjectDesign;

internal sealed class Combined
{
    private int _leftA;
    private int _leftB;
    private int _leftC;
    private int _rightA;
    private int _rightB;
    private int _rightC;

    public void LeftOne() => _leftA = _leftB + 1;
    public void LeftTwo() => _leftB = _leftC + 1;
    public void LeftThree() => _leftC = _leftA + 1;
    public void RightOne() => _rightA = _rightB + 1;
    public void RightTwo() => _rightB = _rightC + 1;
    public void RightThree() => _rightC = _rightA + 1;
}

internal sealed class Position
{
    public int X { get; set; }
    public int Y { get; set; }
    public int Z { get; set; }
}

internal sealed class PositionService
{
    public int Magnitude(Position position) => position.X + position.Y + position.Z;
}

internal sealed class DepA { public int Read() => 1; }
internal sealed class DepB { public int Read() => 1; }
internal sealed class DepC { public int Read() => 1; }
internal sealed class DepD { public int Read() => 1; }
internal sealed class DepE { public int Read() => 1; }
internal sealed class DepF { public int Read() => 1; }

internal sealed class Coordinator
{
    private readonly DepA _a = new();
    private readonly DepB _b = new();
    private readonly DepC _c = new();
    private readonly DepD _d = new();
    private readonly DepE _e = new();
    private readonly DepF _f = new();

    public int First() => _a.Read() + _b.Read();
    public int Second() => _c.Read() + _d.Read();
    public int Third() => _e.Read() + _f.Read();
}

internal sealed class Country
{
    public void Print() { }
}

internal sealed class Address
{
    public Country Country { get; } = new();
}

internal sealed class Customer
{
    public Address Address { get; } = new();
}

internal sealed class Order
{
    public Customer Customer { get; } = new();
}

internal sealed class OrderHandler
{
    public void Handle(Order order) => order.Customer.Address.Country.Print();
}

internal sealed class AccountModel
{
    public int Balance { get; set; }
    public int Limit { get; set; }
    public int Pending { get; set; }
    public int Version { get; set; }

    public void Apply(int amount) => Balance += amount;
}
