using OopDesignChecker.Localization;

namespace OopDesignChecker.Analysis;

/*
{
  "責務": {
    "解析入力例外": "入力に原因がある解析失敗について、原因例外と日英の利用者向け説明を保持します。"
  },
  "フィールド": {
    "_日本語説明": "英語のMessageとは別に保持する利用者向け説明です。"
  }
}
*/
internal sealed class 解析入力例外 : InvalidOperationException
{
    private readonly string _日本語説明;

    /*
    {
      "責務": {
        "解析入力例外": "互換性のため英語のMessageを保持し、日本語表示用の説明も保存します。"
      },
      "処理": [
        "互換性のため英語のMessageを保持し、日本語表示用の説明も保存します。"
      ],
      "引数": {
        "日本語説明": "日本語表示で使用する説明です。",
        "英語説明": "既存のMessageおよび英語表示で使用する説明です。",
        "原因": "入力に起因する元の例外です。"
      },
      "戻り値": []
    }
    */
    public 解析入力例外(string 日本語説明, string 英語説明, Exception? 原因 = null)
        : base(英語説明, 原因)
    {
        _日本語説明 = 日本語説明;
    }

    /*
    {
      "責務": {
        "説明文を選ぶ": "表示言語に対応する入力エラーの説明を返します。"
      },
      "処理": [
        "表示言語に対応する入力エラーの説明を返します。"
      ],
      "引数": {
        "言語": "利用者が指定した表示言語です。"
      },
      "戻り値": "表示言語に対応するエラー説明です。"
    }
    */
    public string 説明文を選ぶ(UserInterfaceLanguage 言語) =>
        言語 == UserInterfaceLanguage.Japanese ? _日本語説明 : Message;
}
