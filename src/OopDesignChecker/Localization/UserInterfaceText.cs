using OopDesignChecker.Core;

namespace OopDesignChecker.Localization;

public static class UserInterfaceText
{
    public static UserInterfaceLanguage DefaultLanguage => UserInterfaceLanguage.Japanese;

    public static bool TryParseLanguage(string? value, out UserInterfaceLanguage language)
    {
        switch (value?.Trim().ToLowerInvariant())
        {
            case "ja":
            case "japanese":
                language = UserInterfaceLanguage.Japanese;
                return true;
            case "en":
            case "english":
                language = UserInterfaceLanguage.English;
                return true;
            default:
                language = default;
                return false;
        }
    }

    public static string Select(UserInterfaceLanguage language, string japanese, string english) =>
        language == UserInterfaceLanguage.Japanese ? japanese : english;

    public static string SeverityName(
        DesignDiagnosticSeverity severity,
        UserInterfaceLanguage language
    ) =>
        language == UserInterfaceLanguage.English
            ? severity.ToString()
            : severity switch
            {
                DesignDiagnosticSeverity.Danger => "危険",
                DesignDiagnosticSeverity.Warning => "警告",
                DesignDiagnosticSeverity.Attention => "注意",
                _ => throw new ArgumentOutOfRangeException(nameof(severity), severity, null),
            };

    public static string RuleTitle(
        string ruleId,
        string englishTitle,
        UserInterfaceLanguage language
    )
    {
        if (language == UserInterfaceLanguage.English)
        {
            return englishTitle;
        }

        return ruleId switch
        {
            "OOP001" => "共通抽象の欠如",
            "OOP002" => "型分岐によるポリモーフィズム迂回",
            "OOP003" => "不要な抽象",
            "OOP101" => "過剰な可視性",
            "OOP102" => "sealed候補",
            "OOP103" => "static member候補",
            "OOP104" => "static class候補",
            "OOP105" => "stateful static設計",
            "OOP106" => "カプセル化漏れ",
            "OOP107" => "オブジェクト不変条件の迂回",
            "OOP108" => "過剰な外部状態操作",
            "OOP201" => "巨大な主処理",
            "OOP301" => "疑わしい継承関係",
            "OOP302" => "親契約の大部分を未使用",
            "OOP303" => "子が親の振る舞いを無効化",
            "OOP304" => "過剰な継承深度",
            "OOP305" => "抽象があるのに具象型へ依存",
            "OOP306" => "回避可能な具象生成依存",
            "OOP307" => "継承よりcompositionが適切な可能性",
            "OOP401" => "1クラス内に複数objectが存在する可能性",
            "OOP402" => "貧血オブジェクト候補",
            "OOP403" => "無関係な依存の過多",
            "OOP404" => "オブジェクト内部への過剰なナビゲーション",
            "OOP405" => "getter/setterだけのobject候補",
            _ => englishTitle,
        };
    }

    public static string DiagnosticMessage(
        string ruleId,
        string englishMessage,
        UserInterfaceLanguage language
    )
    {
        if (language == UserInterfaceLanguage.English)
        {
            return englishMessage;
        }

        return ruleId switch
        {
            "OOP001" => "同じ概念として扱われる複数型に、適切な共通抽象が見当たりません。",
            "OOP002" => "具象型による分岐が、共有抽象によるポリモーフィズムを迂回しています。",
            "OOP003" => "この抽象は共有概念や拡張点としての意味が弱く、不要な可能性があります。",
            "OOP101" => "実際の利用範囲に対して可視性が広すぎる可能性があります。",
            "OOP102" => "継承される意図が確認できないため、sealed化を検討できます。",
            "OOP103" => "instance stateへ依存しないため、static memberとして表現できる可能性があります。",
            "OOP104" => "有意味なinstance stateがないため、static classとして表現できる可能性があります。",
            "OOP105" => "static領域に可変共有状態があり、global mutable stateになっています。",
            "OOP106" => "内部表現が外部へ直接公開され、所有objectを迂回して変更できる可能性があります。",
            "OOP107" => "object自身が守るべき不変条件を外部から迂回できる可能性があります。",
            "OOP108" => "別objectの内部状態を外部から直接操作しすぎています。",
            "OOP201" => "主要operationが大きく複雑で、複数の処理段階を抱えている可能性があります。",
            "OOP301" => "この継承は意味のある親子関係より実装再利用へ寄っている可能性があります。",
            "OOP302" => "子型が親の有効な契約の大部分を利用またはサポートしていません。",
            "OOP303" => "子型が、本来サポートすべき親の振る舞いを無効化または拒否しています。",
            "OOP304" => "project-ownedな継承階層が深く、振る舞いの追跡が難しくなる可能性があります。",
            "OOP305" => "共有抽象で表現できる振る舞いに対して、具象型へ直接依存しています。",
            "OOP306" => "置換可能なcollaboratorを利用側が直接生成しており、具象生成依存を避けられる可能性があります。",
            "OOP307" => "この継承は、意味的なis-a関係よりcompositionで表現する方が適切な可能性があります。",
            "OOP401" => "1つのclass内に、独立して存在できる複数のstate/behavior clusterがある可能性があります。",
            "OOP402" => "stateを持つobjectから有意味なbehaviorが外部へ移りすぎている可能性があります。",
            "OOP403" => "objectが自身のidentityと関係の薄い複数のsubsystemへ依存している可能性があります。",
            "OOP404" => "別objectの内部graphを深く辿っており、内部構造へのcouplingが強くなっています。",
            "OOP405" => "このobjectはgetter/setter中心で、有意味なstate operationが外部にある可能性があります。",
            _ => englishMessage,
        };
    }
}
