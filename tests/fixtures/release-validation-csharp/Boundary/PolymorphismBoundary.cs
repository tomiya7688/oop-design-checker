namespace ReleaseValidation.Boundary.Polymorphism;

internal sealed class CacheEntry
{
    public void Open() { }

    public void Close() { }
}

internal sealed class Window
{
    public void Open() { }

    public void Close() { }
}

internal sealed class Connection
{
    public void Open() { }

    public void Close() { }
}

internal interface IMarker { }

internal sealed class Tagged : IMarker { }

public interface IPlugin
{
    void Run();
}

internal sealed class BuiltInPlugin : IPlugin
{
    public void Run() { }
}
