using OopDesignChecker.Core;
using OopDesignChecker.Rules;

namespace OopDesignChecker.Tests;

internal static class EncapsulationPrecisionSmokeTests
{
    public static void Run()
    {
        FrameworkFacingPublicTypeIsAllowed();
        SerializableCarrierIsAllowedByEncapsulationRule();
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
        ObjectInvariantBypassRule rule,
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
