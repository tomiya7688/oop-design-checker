namespace OopDesignChecker.Analysis;

internal sealed record 解析文脈(
    解析対象プロジェクト 対象プロジェクト,
    IReadOnlyList<解析対象プロジェクト> 対象プロジェクト群,
    CancellationToken キャンセル通知
)
{
    public 解析文脈(解析対象プロジェクト 対象プロジェクト)
        : this(対象プロジェクト, [対象プロジェクト], CancellationToken.None) { }

    public 解析文脈(
        解析対象プロジェクト 対象プロジェクト,
        IReadOnlyList<解析対象プロジェクト> 対象プロジェクト群
    )
        : this(対象プロジェクト, 対象プロジェクト群, CancellationToken.None) { }
}
