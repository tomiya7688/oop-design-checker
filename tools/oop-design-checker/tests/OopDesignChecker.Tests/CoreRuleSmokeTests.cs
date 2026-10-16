using OopDesignChecker.Core;
using OopDesignChecker.Rules;

namespace OopDesignChecker.Tests;

internal static class 基本規則スモークテスト
{
    public static void 実行する()
    {
        共通抽象の不足を検出する();
        シール候補を注意として報告する();
        状態を持つ静的設計は警告になる();
        抽象が足りない具象依存は警告になる();
        回避可能な具象生成は警告になる();
        独立オブジェクトまとまりを警告する();
        プロパティ状態まとまりを警告する();
        補助処理で連結した状態は警告しない();
        継承精度スモークテスト.実行する();
        継承深度精度スモークテスト.実行する();
        多態性精度スモークテスト.実行する();
        カプセル化精度スモークテスト.実行する();
        静的設計精度スモークテスト.実行する();
        操作精度スモークテスト.実行する();
        診断協調スモークテスト.実行する();
        オブジェクト辿り規則テスト.実行する();
        拡張規則スモークテスト.実行する();
    }

    private static void 共通抽象の不足を検出する()
    {
        const string ソース = """
            internal sealed class Dog
            {
                public void Move() { }
                public void Eat() { }
            }

            internal sealed class Cat
            {
                public void Move() { }
                public void Eat() { }
            }

            internal sealed class Bird
            {
                public void Move() { }
                public void Eat() { }
            }

            internal sealed class Zoo
            {
                public void Handle(Dog animal) { }
                public void Handle(Cat animal) { }
                public void Handle(Bird animal) { }
            }
            """;

        単一診断を確認する(
            new 共通抽象不足規則(),
            ソース,
            "OOP001",
            DesignDiagnosticSeverity.Warning
        );
    }

    private static void シール候補を注意として報告する()
    {
        const string ソース = """
            internal class Worker
            {
                private int _count;
                public void Run() => _count++;
            }
            """;

        単一診断を確認する(
            new 封止候補規則(),
            ソース,
            "OOP102",
            DesignDiagnosticSeverity.Attention
        );
    }

    private static void 状態を持つ静的設計は警告になる()
    {
        const string ソース = """
            internal static class GlobalState
            {
                private static int _count;
                public static void Increment() => _count++;
            }
            """;

        単一診断を確認する(
            new 状態保持静的設計規則(),
            ソース,
            "OOP105",
            DesignDiagnosticSeverity.Warning
        );
    }

    private static void 抽象が足りない具象依存は警告になる()
    {
        const string ソース = """
            internal interface IClock
            {
                int Read();
            }

            internal sealed class Clock : IClock
            {
                public int Read() => 0;
            }

            internal sealed class Service
            {
                private readonly Clock _clock;
                public Service(Clock clock) => _clock = clock;
                public int Run() => _clock.Read();
            }
            """;

        単一診断を確認する(
            new 具象型依存規則(),
            ソース,
            "OOP305",
            DesignDiagnosticSeverity.Warning
        );
    }

    private static void 回避可能な具象生成は警告になる()
    {
        const string ソース = """
            internal interface IClock
            {
                int Read();
            }

            internal sealed class Clock : IClock
            {
                public int Read() => 0;
            }

            internal sealed class Service
            {
                private readonly IClock _clock = new Clock();
                public int Run() => _clock.Read();
            }
            """;

        単一診断を確認する(
            new 回避可能な具象生成規則(),
            ソース,
            "OOP306",
            DesignDiagnosticSeverity.Warning
        );
    }

    private static void 独立オブジェクトまとまりを警告する()
    {
        const string ソース = """
            internal sealed class Combined
            {
                private int _leftA;
                private int _leftB;
                private int _leftC;
                private int _rightA;
                private int _rightB;
                private int _rightC;

                public void LeftOne() => _leftA = _leftB + 1;
                public void LeftTwo() => _leftB = _leftC + 1;
                public void LeftThree() => _leftC = _leftA + 1;

                public void RightOne() => _rightA = _rightB + 1;
                public void RightTwo() => _rightB = _rightC + 1;
                public void RightThree() => _rightC = _rightA + 1;
            }
            """;

        単一診断を確認する(
            new 複数オブジェクト混在規則(),
            ソース,
            "OOP401",
            DesignDiagnosticSeverity.Warning
        );
    }

    private static void プロパティ状態まとまりを警告する()
    {
        const string ソース = """
            internal sealed class Combined
            {
                private int LeftA { get; set; }
                private int LeftB { get; set; }
                private int LeftC { get; set; }
                private int RightA { get; set; }
                private int RightB { get; set; }
                private int RightC { get; set; }

                public void LeftOne() => LeftA = LeftB + 1;
                public void LeftTwo() => LeftB = LeftC + 1;
                public void LeftThree() => LeftC = LeftA + 1;

                public void RightOne() => RightA = RightB + 1;
                public void RightTwo() => RightB = RightC + 1;
                public void RightThree() => RightC = RightA + 1;
            }
            """;

        単一診断を確認する(
            new 複数オブジェクト混在規則(),
            ソース,
            "OOP401",
            DesignDiagnosticSeverity.Warning
        );
    }

    private static void 補助処理で連結した状態は警告しない()
    {
        const string ソース = """
            internal sealed class CoherentObject
            {
                private int _leftA;
                private int _leftB;
                private int _leftC;
                private int _rightA;
                private int _rightB;
                private int _rightC;

                public void Run()
                {
                    LeftOne();
                    RightOne();
                }

                private void LeftOne() => _leftA = _leftB + 1;
                private void LeftTwo() => _leftB = _leftC + 1;
                private void LeftThree() => _leftC = _leftA + 1;

                private void RightOne() => _rightA = _rightB + 1;
                private void RightTwo() => _rightB = _rightC + 1;
                private void RightThree() => _rightC = _rightA + 1;
            }
            """;

        診断がないことを確認する(new 複数オブジェクト混在規則(), ソース, "OOP401");
    }

    private static void 単一診断を確認する(
        I解析規則 規則,
        string ソース,
        string 規則ID,
        DesignDiagnosticSeverity 重要度
    )
    {
        var 診断一覧 = 規則.解析する(テスト用プロジェクト作成器.解析文脈を作る(ソース)).ToArray();
        if (診断一覧.Length != 1 || 診断一覧[0].Rule.Id != 規則ID || 診断一覧[0].Severity != 重要度)
        {
            var 検出内容 = string.Join(
                ", ",
                診断一覧.Select(項目 => $"{項目.Rule.Id}:{項目.Severity}")
            );
            throw new InvalidOperationException(
                $"Expected one {規則ID}:{重要度} diagnostic, found [{検出内容}]."
            );
        }
    }

    private static void 診断がないことを確認する(
        複数オブジェクト混在規則 規則,
        string ソース,
        string 規則ID
    )
    {
        var 診断一覧 = 規則.解析する(テスト用プロジェクト作成器.解析文脈を作る(ソース)).ToArray();
        if (診断一覧.Length == 0)
        {
            return;
        }

        var 検出内容 = string.Join(", ", 診断一覧.Select(項目 => 項目.Rule.Id));
        throw new InvalidOperationException(
            $"Expected no {規則ID} diagnostic, found [{検出内容}]."
        );
    }
}
