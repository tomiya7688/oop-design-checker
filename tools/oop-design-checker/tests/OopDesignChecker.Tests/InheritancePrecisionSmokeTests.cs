using System.Globalization;
using OopDesignChecker.Core;
using OopDesignChecker.Rules;

namespace OopDesignChecker.Tests;

internal static class InheritancePrecisionSmokeTests
{
    public static void Run()
    {
        マーカーインターフェースだけでは具象依存の警告を出さない();
        アクセス不能なインターフェースメンバーを抽象候補にしない();
        継承したインスタンス契約を意味のある抽象とみなす();
        internalなインスタンス契約を同一プロジェクト内で利用できる();
        抽象のみを使う利用者へ具象依存を警告する();
        後続の意味ある抽象と利用者の使用を照合する();
        複数の抽象候補を決定的に選ぶ();
        多数のコンストラクター引数で利用状況を再利用する();
        具象型だけの振る舞いを具象依存の根拠にする();
        マーカーインターフェースだけでは生成警告を出さない();
        所有するファクトリーでの生成を許可する();
        入れ子のオブジェクト生成を許可する();
        main内の依存構築を許可する();
        コンポジションルート内の依存構築を許可する();
        業務メソッド内の依存構築を警告する();
        業務式内の具象生成を警告する();
        プロジェクト内の継承公開面置換を検出する();
        外部基底型の公開面置換を許可する();
        実装再利用目的の継承を注意とする();
        コンストラクターだけのprotected利用を許可する();
        正当な特化をコンポジション候補にしない();
        ParentContractPrecisionSmokeTests.Run();
    }

    private static void マーカーインターフェースだけでは具象依存の警告を出さない()
    {
        const string source = """
            internal interface IMarker
            {
            }

            internal sealed class TaggedService : IMarker
            {
                public void Run() { }
            }

            internal sealed class Consumer
            {
                public Consumer(TaggedService service) { }
            }
            """;

        診断がないことを確認する(new ConcreteTypeDependencyRule(), source, "OOP305");
    }

    private static void アクセス不能なインターフェースメンバーを抽象候補にしない()
    {
        const string source = """
            internal interface IPrivateHelperOnly
            {
                private void Reset() { }
            }

            internal interface IStaticOnly
            {
                static void Configure() { }
                static int Count => 0;
                static event System.Action? Changed
                {
                    add { }
                    remove { }
                }
            }

            internal sealed class PrivateHelperService : IPrivateHelperOnly
            {
                public void Run() { }
            }

            internal sealed class StaticContractService : IStaticOnly
            {
                public void Run() { }
            }

            internal sealed class Consumer
            {
                public Consumer(PrivateHelperService privateHelper, StaticContractService staticContract) { }

                public void Run()
                {
                    _ = new PrivateHelperService();
                    _ = new StaticContractService();
                }
            }
            """;

        診断がないことを確認する(new ConcreteTypeDependencyRule(), source, "OOP305");
        診断がないことを確認する(new AvoidableConcreteConstructionRule(), source, "OOP306");
    }

    private static void 継承したインスタンス契約を意味のある抽象とみなす()
    {
        const string source = """
            internal interface IReadOnlyClock
            {
                int Read();
            }

            internal interface IClock : IReadOnlyClock
            {
                private void ResetCache() { }
                static void Configure() { }
            }

            internal sealed class Clock : IClock
            {
                public int Read() => 0;
            }

            internal sealed class Consumer
            {
                private readonly Clock _clock;

                public Consumer(Clock clock)
                {
                    _clock = clock;
                }

                public int Run() => _clock.Read();
            }

            internal sealed class Worker
            {
                public int Run()
                {
                    _ = new Clock();
                    return 0;
                }
            }
            """;

        診断が1件であることを確認する(
            new ConcreteTypeDependencyRule(),
            source,
            "OOP305",
            DesignDiagnosticSeverity.Warning
        );
        診断が1件であることを確認する(
            new AvoidableConcreteConstructionRule(),
            source,
            "OOP306",
            DesignDiagnosticSeverity.Warning
        );
    }

    private static void internalなインスタンス契約を同一プロジェクト内で利用できる()
    {
        const string source = """
            internal interface IAssemblyContract
            {
                internal void Reset() { }
            }

            internal sealed class Service : IAssemblyContract
            {
                public void Run() { }
            }

            internal sealed class Worker
            {
                public void Use(IAssemblyContract service) => service.Reset();

                public void Run()
                {
                    _ = new Service();
                }
            }
            """;

        診断が1件であることを確認する(
            new AvoidableConcreteConstructionRule(),
            source,
            "OOP306",
            DesignDiagnosticSeverity.Warning
        );
    }

    private static void 抽象のみを使う利用者へ具象依存を警告する()
    {
        const string source = """
            internal interface IClock
            {
                int Read();
            }

            internal sealed class Clock : IClock
            {
                public int Read() => 0;
                public void Calibrate() { }
            }

            internal sealed class Service
            {
                private readonly Clock _clock;

                public Service(Clock clock)
                {
                    _clock = clock;
                }

                public int Run() => _clock.Read();
            }
            """;

        診断が1件であることを確認する(
            new ConcreteTypeDependencyRule(),
            source,
            "OOP305",
            DesignDiagnosticSeverity.Warning
        );
    }

    private static void 後続の意味ある抽象と利用者の使用を照合する()
    {
        const string source = """
            internal interface IClock
            {
                int Read();
            }

            internal interface IResettable
            {
                void Reset();
            }

            internal sealed class Clock : IClock, IResettable
            {
                public int Read() => 0;
                public void Reset() { }
            }

            internal sealed class Service
            {
                private readonly Clock _clock;

                public Service(Clock clock)
                {
                    _clock = clock;
                }

                public void Run() => _clock.Reset();
            }
            """;

        診断が1件であることを確認する(
            new ConcreteTypeDependencyRule(),
            source,
            "OOP305",
            DesignDiagnosticSeverity.Warning
        );
    }

    private static void 複数の抽象候補を決定的に選ぶ()
    {
        const string source = """
            internal interface IZeta
            {
                int Read();
            }

            internal interface IAlpha
            {
                int Read();
            }

            internal sealed class Clock : IZeta, IAlpha
            {
                public int Read() => 0;
            }

            internal sealed class Service
            {
                private readonly Clock _clock;

                public Service(Clock clock)
                {
                    _clock = clock;
                }

                public int Run() => _clock.Read();
            }
            """;

        var diagnostic = new ConcreteTypeDependencyRule()
            .解析する(TestProjectFactory.解析文脈を作る(source))
            .Single(item => item.Rule.Id == "OOP305");
        if (!diagnostic.Message.Contains("project abstraction IAlpha", StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"Expected deterministic IAlpha candidate in the diagnostic, found: {diagnostic.Message}"
            );
        }
    }

    private static void 具象型だけの振る舞いを具象依存の根拠にする()
    {
        const string source = """
            internal interface IClock
            {
                int Read();
            }

            internal interface IResettable
            {
                void Reset();
            }

            internal sealed class Clock : IClock, IResettable
            {
                public int Read() => 0;
                public void Calibrate() { }
                public void Reset() { }
            }

            internal sealed class Service
            {
                private readonly Clock _clock;

                public Service(Clock clock)
                {
                    _clock = clock;
                }

                public void CalibrateClock() => _clock.Calibrate();
            }
            """;

        診断がないことを確認する(new ConcreteTypeDependencyRule(), source, "OOP305");
    }

    private static void 多数のコンストラクター引数で利用状況を再利用する()
    {
        var source = new System.Text.StringBuilder();
        const int dependencyCount = 8;
        const int unrelatedTypeCount = 200;

        for (var index = 0; index < dependencyCount; index++)
        {
            source.AppendLine(
                CultureInfo.InvariantCulture,
                $"internal interface IContract{index} {{ int Read{index}(); }}"
            );
            source.AppendLine(
                CultureInfo.InvariantCulture,
                $"internal sealed class Dependency{index} : IContract{index} {{ public int Read{index}() => 0; }}"
            );
            source.AppendLine(
                CultureInfo.InvariantCulture,
                $"internal sealed partial class Consumer {{ private readonly Dependency{index} _dependency{index}; }}"
            );
        }

        for (var index = 0; index < unrelatedTypeCount; index++)
        {
            source.AppendLine(
                CultureInfo.InvariantCulture,
                $"internal sealed class Unrelated{index} {{ public int Read() => {index}; }}"
            );
        }

        source.AppendLine("internal sealed partial class Consumer");
        source.AppendLine("{");
        source.AppendLine(
            CultureInfo.InvariantCulture,
            $"    public Consumer({string.Join(", ", Enumerable.Range(0, dependencyCount).Select(index => string.Create(CultureInfo.InvariantCulture, $"Dependency{index} dependency{index}")))})"
        );
        source.AppendLine("    {");
        for (var index = 0; index < dependencyCount; index++)
        {
            source.AppendLine(
                CultureInfo.InvariantCulture,
                $"        _dependency{index} = dependency{index};"
            );
        }

        source.AppendLine("    }");
        for (var index = 0; index < dependencyCount; index++)
        {
            source.AppendLine(
                CultureInfo.InvariantCulture,
                $"    public int Use{index}() => _dependency{index}.Read{index}();"
            );
        }

        source.AppendLine("}");

        var diagnostics = new ConcreteTypeDependencyRule()
            .解析する(TestProjectFactory.解析文脈を作る(source.ToString()))
            .Where(diagnostic => diagnostic.Rule.Id == "OOP305")
            .ToArray();

        if (diagnostics.Length != dependencyCount)
        {
            throw new InvalidOperationException(
                $"Expected {dependencyCount} OOP305 diagnostics for a partial consumer with multiple constructor parameters, found {diagnostics.Length}."
            );
        }
    }

    private static void マーカーインターフェースだけでは生成警告を出さない()
    {
        const string source = """
            internal interface IMarker
            {
            }

            internal sealed class TaggedService : IMarker
            {
                public void Run() { }
            }

            internal sealed class Consumer
            {
                private readonly TaggedService _service = new();
            }
            """;

        診断がないことを確認する(new AvoidableConcreteConstructionRule(), source, "OOP306");
    }

    private static void 所有するファクトリーでの生成を許可する()
    {
        const string source = """
            internal interface IClock
            {
                int Read();
            }

            internal sealed class Clock : IClock
            {
                public int Read() => 0;
            }

            internal sealed class BackupClock : IClock
            {
                public int Read() => 1;
            }

            internal sealed class ClockFactory
            {
                public IClock Create() => new Clock();
            }
            """;

        診断がないことを確認する(new AvoidableConcreteConstructionRule(), source, "OOP306");
    }

    private static void 入れ子のオブジェクト生成を許可する()
    {
        const string source = """
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
                public Service(IClock clock) { }
            }

            internal sealed class Bootstrap
            {
                private readonly Service _service = new Service(new Clock());
            }
            """;

        診断がないことを確認する(new AvoidableConcreteConstructionRule(), source, "OOP306");
    }

    private static void main内の依存構築を許可する()
    {
        const string source = """
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
                public Service(IClock clock) { }
            }

            internal static class Program
            {
                public static void Main()
                {
                    var clock = new Clock();
                    var service = new Service(clock);
                }
            }
            """;

        診断がないことを確認する(new AvoidableConcreteConstructionRule(), source, "OOP306");
    }

    private static void コンポジションルート内の依存構築を許可する()
    {
        const string source = """
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
                public Service(IClock clock) { }
            }

            internal sealed class AppCompositionRoot
            {
                public Service Build()
                {
                    var clock = new Clock();
                    return new Service(clock);
                }
            }
            """;

        診断がないことを確認する(new AvoidableConcreteConstructionRule(), source, "OOP306");
    }

    private static void 業務メソッド内の依存構築を警告する()
    {
        const string source = """
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
                public Service(IClock clock) { }
            }

            internal sealed class Worker
            {
                public Service Build()
                {
                    var clock = new Clock();
                    return new Service(clock);
                }
            }
            """;

        診断が1件であることを確認する(
            new AvoidableConcreteConstructionRule(),
            source,
            "OOP306",
            DesignDiagnosticSeverity.Warning
        );
    }

    private static void 業務式内の具象生成を警告する()
    {
        const string source = """
            internal interface IClock
            {
                int Read();
            }

            internal sealed class Clock : IClock
            {
                public int Read() => 0;
            }

            internal sealed class Worker
            {
                public int ReadNow() => new Clock().Read();
            }
            """;

        診断が1件であることを確認する(
            new AvoidableConcreteConstructionRule(),
            source,
            "OOP306",
            DesignDiagnosticSeverity.Warning
        );
    }

    private static void プロジェクト内の継承公開面置換を検出する()
    {
        const string source = """
            internal class Device
            {
                public void Start() { }
                public int Status => 0;
            }

            internal sealed class SpecializedDevice : Device
            {
                public new void Start() { }
                public new int Status => 1;
            }
            """;

        診断が1件であることを確認する(
            new 疑わしい継承関係規則(),
            source,
            "OOP301",
            DesignDiagnosticSeverity.Warning
        );
    }

    private static void 外部基底型の公開面置換を許可する()
    {
        const string source = """
            internal sealed class CustomException : System.Exception
            {
                public new string Message => "custom";
                public new string Source
                {
                    get => "custom";
                    set { }
                }
            }
            """;

        診断がないことを確認する(new 疑わしい継承関係規則(), source, "OOP301");
    }

    private static void 実装再利用目的の継承を注意とする()
    {
        const string source = """
            internal class CalculatorBase
            {
                protected int ReadLeft() => 1;
                protected int ReadRight() => 2;
            }

            internal sealed class TotalCalculator : CalculatorBase
            {
                public int 計算する() => ReadLeft() + ReadRight();
            }
            """;

        診断が1件であることを確認する(
            new CompositionCandidateRule(),
            source,
            "OOP307",
            DesignDiagnosticSeverity.Attention
        );
    }

    private static void コンストラクターだけのprotected利用を許可する()
    {
        const string source = """
            internal class ConfigurationBase
            {
                protected int Left;
                protected int Right;
            }

            internal sealed class Configuration : ConfigurationBase
            {
                public Configuration(int left, int right)
                {
                    Left = left;
                    Right = right;
                }
            }
            """;

        診断がないことを確認する(new CompositionCandidateRule(), source, "OOP307");
    }

    private static void 正当な特化をコンポジション候補にしない()
    {
        const string source = """
            internal class Processor
            {
                protected int ReadLeft() => 1;
                protected int ReadRight() => 2;
                public virtual int Run() => 0;
            }

            internal sealed class SpecializedProcessor : Processor
            {
                public override int Run() => ReadLeft() + ReadRight();
            }
            """;

        診断がないことを確認する(new CompositionCandidateRule(), source, "OOP307");
    }

    private static void 診断が1件であることを確認する(
        I解析規則 rule,
        string source,
        string ruleId,
        DesignDiagnosticSeverity severity
    )
    {
        var diagnostics = rule.解析する(TestProjectFactory.解析文脈を作る(source)).ToArray();
        if (
            diagnostics.Length == 1
            && diagnostics[0].Rule.Id == ruleId
            && diagnostics[0].Severity == severity
        )
        {
            return;
        }

        var found = string.Join(
            ", ",
            diagnostics.Select(item => $"{item.Rule.Id}:{item.Severity}")
        );
        throw new InvalidOperationException(
            $"Expected one {ruleId}:{severity} diagnostic, found [{found}]."
        );
    }

    private static void 診断がないことを確認する(I解析規則 rule, string source, string ruleId)
    {
        var diagnostics = rule.解析する(TestProjectFactory.解析文脈を作る(source)).ToArray();
        if (diagnostics.Length == 0)
        {
            return;
        }

        var found = string.Join(", ", diagnostics.Select(item => item.Rule.Id));
        throw new InvalidOperationException($"Expected no {ruleId} diagnostic, found [{found}].");
    }
}
