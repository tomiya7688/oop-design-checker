namespace OopDesignChecker.Gui;

internal static class ClipboardOperation
{
    public static async Task<bool> TrySetTextAsync(Func<Task> setTextAsync)
    {
        try
        {
            await setTextAsync();
            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }
}
