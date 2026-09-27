namespace ReleaseValidation.Positive.Polymorphism;

internal sealed class Dog
{
    public void Move() { }

    public void Eat() { }
}

internal sealed class Cat
{
    public void Move() { }

    public void Eat() { }
}

internal sealed class Bird
{
    public void Move() { }

    public void Eat() { }
}

internal sealed class Zoo
{
    public void Handle(Dog animal) { }

    public void Handle(Cat animal) { }

    public void Handle(Bird animal) { }
}

internal interface IRunner
{
    void Run();
}

internal sealed class FastRunner : IRunner
{
    public void Run() { }
}

internal sealed class SlowRunner : IRunner
{
    public void Run() { }
}

internal sealed class RunnerCaller
{
    public void First(IRunner runner)
    {
        if (runner is FastRunner)
        {
            ((FastRunner)runner).Run();
        }
        else if (runner is SlowRunner)
        {
            ((SlowRunner)runner).Run();
        }
    }

    public void Second(IRunner runner)
    {
        if (runner is FastRunner)
        {
            ((FastRunner)runner).Run();
        }
        else if (runner is SlowRunner)
        {
            ((SlowRunner)runner).Run();
        }
    }
}

internal interface IUnused
{
    void Execute();
}

internal sealed class OnlyImplementation : IUnused
{
    public void Execute() { }
}
