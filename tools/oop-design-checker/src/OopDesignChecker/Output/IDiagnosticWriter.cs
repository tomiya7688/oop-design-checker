using OopDesignChecker.Core;

namespace OopDesignChecker.Output;

/*
{
  "責務": {
    "診断書き込み器": "各形式に共通する診断一覧の出力だけを契約にします。形式固有の設定は具体的な書き込み器が所有します。"
  }
}
*/
internal interface 診断書き込み器
{
    /*
    {
      "責務": {
        "Write": "書き込み器が保持する形式と出力先で診断一覧を出力します。"
      },
      "処理": [
        "実装ごとの形式で診断一覧を出力します。"
      ],
      "引数": {
        "diagnostics": "出力する診断一覧です。"
      },
      "戻り値": []
    }
    */
    void Write(IReadOnlyList<DesignDiagnostic> diagnostics);
}
