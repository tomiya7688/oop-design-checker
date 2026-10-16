namespace OopDesignChecker.Analysis;

internal static class プロジェクト読み込み器生成器
{
    public static Iプロジェクト読み込み器 生成する(IReadOnlyList<string> 無視対象パス群) =>
        new CSharpプロジェクト読み込み器(無視対象パス群);
}
