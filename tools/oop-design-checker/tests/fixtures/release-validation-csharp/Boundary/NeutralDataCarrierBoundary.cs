namespace ReleaseValidation.Boundary.NeutralDataCarrier;

internal sealed class Person
{
    public string Name { get; init; } = "";
    public int Age { get; init; }
    public string City { get; init; } = "";
    public string Country { get; init; } = "";
}

internal static class ReportBuilder
{
    public static string Build(Person value) => $"{value.Name}:{value.Age}:{value.City}";
}
