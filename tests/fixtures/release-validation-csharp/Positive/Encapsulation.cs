namespace ReleaseValidation.Positive.Encapsulation;

internal sealed class LeakyBuffer
{
    public readonly List<int> Items = new();
}

internal sealed class Account
{
    public int Balance { get; set; }

    public void ChangeBalance(int value)
    {
        if (value < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(value));
        }

        Balance = value;
    }
}

internal sealed class Player
{
    public int X { get; set; }
    public int Y { get; set; }
    public int Hp { get; set; }
}

internal sealed class PlayerEditor
{
    public void Reset(Player player)
    {
        player.X = 0;
        player.Y = 0;
        player.Hp = 100;
    }
}
