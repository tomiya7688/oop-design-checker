namespace OopDesignChecker.Output;

/*
{
  "責務": {"診断出力形式": "共有writerが提供する出力形式を定義します。CLIの文字列解析と保存可能な形式の制限は、それぞれの呼出境界が扱います。"}
}
*/
internal enum 診断出力形式
{
    Text,
    Json,
    Sarif,
    GitHub,
}
