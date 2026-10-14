using System.Reflection;
using System.Reflection.Emit;
using OopDesignChecker;

namespace OopDesignChecker.Tests;

internal static class ProductVersionResolverTests
{
    public static void Run()
    {
        InformationalVersionDropsBuildMetadata();
        AssemblyVersionIsUsedWhenInformationalVersionIsMissing();
        UnknownIsReturnedWhenBothVersionsAreMissing();
        GuiVersionUsesGuiAssembly();
    }

    private static void InformationalVersionDropsBuildMetadata()
    {
        var assembly = CreateAssembly(
            "InformationalVersionFixture",
            new Version(2, 3, 4, 0),
            "5.6.7-preview.2+build.9"
        );

        AssertVersion("5.6.7-preview.2", ProductVersionResolver.Resolve(assembly));
    }

    private static void AssemblyVersionIsUsedWhenInformationalVersionIsMissing()
    {
        var assembly = CreateAssembly("AssemblyVersionFixture", new Version(8, 9, 10, 11));

        AssertVersion("8.9.10", ProductVersionResolver.Resolve(assembly));
    }

    private static void UnknownIsReturnedWhenBothVersionsAreMissing() =>
        AssertVersion("unknown", ProductVersionResolver.Resolve(null, null));

    private static void GuiVersionUsesGuiAssembly()
    {
        var expected = ProductVersionResolver.Resolve(
            typeof(OopDesignChecker.Gui.Program).Assembly
        );
        var originalOut = Console.Out;
        using var output = new StringWriter();
        try
        {
            Console.SetOut(output);
            OopDesignChecker.Gui.Program.Main(["--version"]);
        }
        finally
        {
            Console.SetOut(originalOut);
        }

        AssertVersion(expected, output.ToString().Trim());
    }

    private static AssemblyBuilder CreateAssembly(
        string name,
        Version? assemblyVersion,
        string? informationalVersion = null
    )
    {
        var assemblyName = new AssemblyName(name) { Version = assemblyVersion };
        var assembly = AssemblyBuilder.DefineDynamicAssembly(
            assemblyName,
            AssemblyBuilderAccess.Run
        );

        if (informationalVersion is not null)
        {
            var attributeConstructor =
                typeof(AssemblyInformationalVersionAttribute).GetConstructor([typeof(string)]);
            if (attributeConstructor is null)
            {
                throw new InvalidOperationException(
                    "AssemblyInformationalVersionAttribute constructor was not found."
                );
            }

            assembly.SetCustomAttribute(
                new CustomAttributeBuilder(attributeConstructor, [informationalVersion])
            );
        }

        return assembly;
    }

    private static void AssertVersion(string expected, string actual)
    {
        if (!string.Equals(expected, actual, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"Expected product version '{expected}', found '{actual}'."
            );
        }
    }
}
