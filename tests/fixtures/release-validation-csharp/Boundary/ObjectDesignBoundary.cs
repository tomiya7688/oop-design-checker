namespace ReleaseValidation.Boundary.ObjectDesign;

internal sealed class CoherentObject
{
    private int _leftA;
    private int _leftB;
    private int _leftC;
    private int _rightA;
    private int _rightB;
    private int _rightC;

    public void Run()
    {
        LeftOne();
        RightOne();
    }

    private void LeftOne() => _leftA = _leftB + 1;
    private void LeftTwo() => _leftB = _leftC + 1;
    private void LeftThree() => _leftC = _leftA + 1;
    private void RightOne() => _rightA = _rightB + 1;
    private void RightTwo() => _rightB = _rightC + 1;
    private void RightThree() => _rightC = _rightA + 1;
}

internal sealed class Person
{
    public string Name { get; set; } = string.Empty;
    public int Age { get; set; }
    public string City { get; set; } = string.Empty;
}

internal sealed class PersonMapper
{
    public string Map(Person person) => $"{person.Name}:{person.Age}:{person.City}";
}

internal sealed class A { public int Read() => 1; }
internal sealed class B { public int Read() => 1; }
internal sealed class C { public int Read() => 1; }
internal sealed class D { public int Read() => 1; }
internal sealed class E { public int Read() => 1; }
internal sealed class F { public int Read() => 1; }

internal sealed class ConnectedCoordinator
{
    private readonly A _a = new();
    private readonly B _b = new();
    private readonly C _c = new();
    private readonly D _d = new();
    private readonly E _e = new();
    private readonly F _f = new();

    public int Run() => First() + Second() + Third();
    private int First() => _a.Read() + _b.Read();
    private int Second() => _c.Read() + _d.Read();
    private int Third() => _e.Read() + _f.Read();
}
