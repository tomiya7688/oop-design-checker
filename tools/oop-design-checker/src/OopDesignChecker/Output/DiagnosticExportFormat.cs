namespace OopDesignChecker.Output;

/*
{
  "責務": {"DiagnosticExportFormat": "GUIなどから診断ファイルを保存できる形式を定義します。既存の公開型と数値を保持し、textとGitHub注釈は保存形式に含めません。"}
}
*/
public enum DiagnosticExportFormat
{
    Json,
    Sarif,
}
