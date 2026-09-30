using OopDesignChecker.Core;
using OopDesignChecker.Rules;

namespace OopDesignChecker.Tests;

internal static class EncapsulationPrecisionSmokeTests
{
    public static void Run()
    {
        FrameworkFacingPublicTypeIsAllowed();
        SerializableCarrierIsAllowedByEncapsulationRule();
        PrivateNestedMutableFieldsAreNotExposed();
        PrivateNestedCollectionAndSetterAreNotExposed();
        PrivateNestedInsidePrivateNestedIsNotExposed();
        PublicMutableFieldRemainsDanger();
        InternalMutableFieldRemainsDanger();
        ProtectedNestedMutableFieldRemainsDanger();
        PrivateProtectedNestedMutableFieldRemainsDanger();
        MessageCarrierExternalWritesAreAllowed();
        ValidatingPublicSetterDoesNotBypassInvariant();
        UnguardedPublicSetterStillBypassesInvariant();
    }

    private static void FrameworkFacingPublicTypeIsAllowed()
    {
        const string source = """
            public sealed class SampleException : System.Exception
            {
            }
            """;

        AssertNone(new ExcessiveVisibilityRule(), source, "OOP101");
    }

    private static void SerializableCarrierIsAllowedByEncapsulationRule()
    {
        const string source = """
            using System.Collections.Generic;

            [System.Serializable]
            internal sealed class Snapshot
            {
                public int Version;
                public List<int> Values { get; set; } = new();
            }
            """;

        AssertNone(new EncapsulationLeakRule(), source, "OOP106");
    }

    private static void PrivateNestedMutableFieldsAreNotExposed()
    {
        const string source = """
            public sealed class Owner
            {
                private struct State
                {
                    public int X;
                    public int Y;
                }

                private State _state;

                public void Set(int value)
                {
                    _state.X = value;
                }

                public int Read() => _state.X + _state.Y;
            }
            """;

        AssertNone(new EncapsulationLeakRule(), source, "OOP106");
    }

    private static void PrivateNestedCollectionAndSetterAreNotExposed()
    {
        const string source = """
            using System.Collections.Generic;

            public sealed class Owner
            {
                private sealed class State
                {
                    private readonly List<int> _values = new();

                    public List<int> Values => _values;

                    public int Count { get; set; }
                }

                private readonly State _state = new();

                public int Count => _state.Count;
            }
            """;

        AssertNone(new EncapsulationLeakRule(), source, "OOP106");
    }

    private static void PrivateNestedInsidePrivateNestedIsNotExposed()
    {
        const string source = """
            public sealed class Owner
            {
                private sealed class Container
                {
                    private struct State
                    {
                        public int Value;
                    }

                    private State _state;

                    public int Read() => _state.Value;
                }

                private readonly Container _container = new();

                public int Read() => _container.Read();
            }
            """;

        AssertNone(new EncapsulationLeakRule(), source, "OOP106");
    }

    private static void PublicMutableFieldRemainsDanger()
    {
        const string source = """
            public struct PublicState
            {
                public int Value;
            }
            """;

        AssertSingle(
            new EncapsulationLeakRule(),
            source,
            "OOP106",
            DesignDiagnosticSeverity.Danger
        );
    }

    private static void InternalMutableFieldRemainsDanger()
    {
        const string source = """
            internal struct InternalState
            {
                public int Value;
            }
            """;

        AssertSingle(
            new EncapsulationLeakRule(),
            source,
            "OOP106",
            DesignDiagnosticSeverity.Danger
        );
    }

    private static void ProtectedNestedMutableFieldRemainsDanger()
    {
        const string source = """
            public class Owner
            {
                protected struct State
                {
                    public int Value;
                }

                protected State Create() => new();
            }
            """;

        AssertSingle(
            new EncapsulationLeakRule(),
            source,
            "OOP106",
            DesignDiagnosticSeverity.Danger
        );
    }

    private static void PrivateProtectedNestedMutableFieldRemainsDanger()
    {
        const string source = """
            public class Owner
            {
                private protected struct State
                {
                    public int Value;
                }

                private protected State Create() => new();
            }
            """;

        AssertSingle(
            new EncapsulationLeakRule(),
            source,
            "OOP106",
            DesignDiagnosticSeverity.Danger
        );
    }

    private static void MessageCarrierExternalWritesAreAllowed()
    {
        const string source = """
            internal sealed class UserCreatedMessage
            {
                public string UserId { get; set; } = string.Empty;
                public string Name { get; set; } = string.Empty;
                public string Email { get; set; } = string.Empty;
            }

            internal sealed class MessageFactory
            {
                public void Fill(UserCreatedMessage message)
                {
                    message.UserId = "1";
                    message.Name = "name";
                    message.Email = "mail@example.com";
                }
            }
            """;

        AssertNone(new ExcessiveExternalStateManipulationRule(), source, "OOP108");
    }

    private static void ValidatingPublicSetterDoesNotBypassInvariant()
    {
        const string source = """
            using System;

            internal sealed class Account
            {
                private int _balance;

                public int Balance
                {
                    get => _balance;
                    set
                    {
                        if (value < 0)
                        {
                            throw new ArgumentOutOfRangeException(nameof(value));
                        }

                        _balance = value;
                    }
                }

                public void ChangeBalance(int value)
                {
                    if (value < 0)
                    {
                        throw new ArgumentOutOfRangeException(nameof(value));
                    }

                    Balance = value;
                }
            }
            """;

        AssertNone(new ObjectInvariantBypassRule(), source, "OOP107");
    }

    private static void UnguardedPublicSetterStillBypassesInvariant()
    {
        const string source = """
            using System;

            internal sealed class Account
            {
                public int Balance { get; set; }

                public void ChangeBalance(int value)
                {
                    if (value < 0)
                    {
                        throw new ArgumentOutOfRangeException(nameof(value));
                    }

                    Balance = value;
                }
            }
            """;

        AssertSingle(
            new ObjectInvariantBypassRule(),
            source,
            "OOP107",
            DesignDiagnosticSeverity.Danger
        );
    }

    private static void AssertNone(IAnalysisRule rule, string source, string ruleId)
    {
        var diagnostics = rule.Analyze(TestProjectFactory.Create(source)).ToArray();
        if (diagnostics.Length == 0)
        {
            return;
        }

        var found = string.Join(", ", diagnostics.Select(item => item.Rule.Id));
        throw new InvalidOperationException($"Expected no {ruleId} diagnostic, found [{found}].");
    }

    private static void AssertSingle(
        IAnalysisRule rule,
        string source,
        string ruleId,
        DesignDiagnosticSeverity severity
    )
    {
        var diagnostics = rule.Analyze(TestProjectFactory.Create(source)).ToArray();
        if (
            diagnostics.Length != 1
            || diagnostics[0].Rule.Id != ruleId
            || diagnostics[0].Severity != severity
        )
        {
            var found = string.Join(
                ", ",
                diagnostics.Select(item => $"{item.Rule.Id}:{item.Severity}")
            );
            throw new InvalidOperationException(
                $"Expected one {ruleId}:{severity} diagnostic, found [{found}]."
            );
        }
    }
}
