namespace OopDesignChecker.Gui;

internal static class クリップボード操作
{
    public static async Task<bool> テキスト設定を試す非同期(Func<Task> setTextAsync)
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
