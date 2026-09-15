namespace OopDesignChecker.Analysis;

internal sealed record OperationMetrics(
    int LineCount,
    int StatementCount,
    int CyclomaticComplexity,
    int MaxNestingDepth,
    int LocalVariableCount,
    int StateMemberCount,
    int TopLevelStatementCount
);
