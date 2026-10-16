namespace OopDesignChecker.Analysis;

internal sealed record 処理量指標(
    int 行数,
    int 文数,
    int 循環的複雑度,
    int 最大ネスト深度,
    int ローカル変数数,
    int 状態メンバー数,
    int トップレベル文数
);
