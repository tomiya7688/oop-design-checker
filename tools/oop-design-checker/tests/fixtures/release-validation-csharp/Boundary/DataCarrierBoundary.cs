namespace ReleaseValidation.Boundary.DataCarrier;

[Serializable]
internal sealed class Snapshot
{
    public int X { get; set; }
    public int Y { get; set; }
    public int Z { get; set; }
    public int Version { get; set; }
}

internal sealed class SnapshotService
{
    public int Sum(Snapshot snapshot) => snapshot.X + snapshot.Y + snapshot.Z;
}
