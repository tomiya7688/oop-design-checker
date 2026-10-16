namespace OopDesignChecker.Application;

internal sealed record コマンドライン解析結果(
    bool IsSuccess,
    コマンドライン引数? Options,
    string? ErrorMessage
)
{
    public static コマンドライン解析結果 Success(コマンドライン引数 options) =>
        new(true, options, null);

    public static コマンドライン解析結果 Failure(string errorMessage) =>
        new(false, null, errorMessage);
}
