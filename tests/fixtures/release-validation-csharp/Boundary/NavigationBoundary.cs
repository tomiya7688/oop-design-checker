namespace ReleaseValidation.Boundary.Navigation;

internal sealed class Event
{
    public DateTime CreatedAt { get; } = DateTime.UtcNow;
}

internal sealed class Handler
{
    public string Handle(Event item) => item.CreatedAt.Date.TimeOfDay.TotalSeconds.ToString();
}
