using OopDesignChecker.Localization;

namespace OopDesignChecker.Analysis;

public enum AnalysisFailureKind
{
    Target,
    Configuration,
    ProjectLoad,
    Compilation,
    Permission,
    Output,
    Internal,
}

/*
{
  "責務": {
    "AnalysisFailureException": "利用者が修正場所を判断できるように、解析失敗の種別・対象パス・日英の説明を保持します。"
  },
  "フィールド": {
    "_日本語説明": "日本語UIで表示する利用者向け説明です。",
    "Kind": "GUIやCUIが表示先と要約を決めるための失敗種別です。",
    "Path": "原因に直接関係する対象または設定のパスです。"
  }
}
*/
public class AnalysisFailureException : InvalidOperationException
{
    private readonly string _日本語説明;

    public AnalysisFailureKind Kind { get; }

    public string? Path { get; }

    public AnalysisFailureException(
        AnalysisFailureKind kind,
        string japaneseMessage,
        string englishMessage,
        string? path = null,
        Exception? innerException = null
    )
        : base(englishMessage, innerException)
    {
        Kind = kind;
        Path = path;
        _日本語説明 = japaneseMessage;
    }

    public string GetLocalizedMessage(UserInterfaceLanguage language) =>
        language == UserInterfaceLanguage.Japanese ? _日本語説明 : Message;
}

/*
{
  "責務": {
    "解析入力例外": "既存の内部呼び出し形を維持しつつ、構造化された解析失敗として入力エラーを伝えます。"
  },
  "フィールド": []
}
*/
internal sealed class 解析入力例外 : AnalysisFailureException
{
    public 解析入力例外(
        string 日本語説明,
        string 英語説明,
        Exception? 原因 = null,
        AnalysisFailureKind 種別 = AnalysisFailureKind.Target,
        string? パス = null
    )
        : base(種別, 日本語説明, 英語説明, パス, 原因) { }
}
