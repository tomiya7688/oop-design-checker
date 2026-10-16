namespace OopDesignChecker.Analysis;

internal interface Iプロジェクト読み込み器
{
    IReadOnlyList<解析対象プロジェクト> プロジェクト群を読み込む(
        string 対象パス,
        CancellationToken cancellationToken = default
    );
}
