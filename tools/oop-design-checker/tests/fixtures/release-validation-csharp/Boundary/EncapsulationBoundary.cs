namespace ReleaseValidation.Boundary.Encapsulation;

[Serializable]
internal sealed class TransferDto
{
    public int X { get; set; }
    public int Y { get; set; }
    public int Z { get; set; }
}

internal sealed class EncapsulatedAccount
{
    private int _balance;

    public int Balance => _balance;

    public void ChangeBalance(int value)
    {
        if (value < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(value));
        }

        _balance = value;
    }
}
