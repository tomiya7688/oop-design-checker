using Microsoft.CodeAnalysis;

namespace OopDesignChecker.Analysis;

internal static class MetadataReferenceProvider
{
    public static IReadOnlyList<MetadataReference> CreatePlatformReferences()
    {
        var trustedAssemblies = AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES") as string;
        if (!string.IsNullOrWhiteSpace(trustedAssemblies))
        {
            return trustedAssemblies
                .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries)
                .Select(path => MetadataReference.CreateFromFile(path))
                .ToArray();
        }

        var bundledReferenceDirectory = Path.Combine(
            AppContext.BaseDirectory,
            "ReferenceAssemblies"
        );
        if (Directory.Exists(bundledReferenceDirectory))
        {
            var bundledReferences = Directory
                .EnumerateFiles(bundledReferenceDirectory, "*.dll", SearchOption.TopDirectoryOnly)
                .OrderBy(path => path, StringComparer.Ordinal)
                .Select(path => MetadataReference.CreateFromFile(path))
                .ToArray();
            if (bundledReferences.Length > 0)
            {
                return bundledReferences;
            }
        }

        throw new InvalidOperationException(
            "The runtime did not provide trusted platform assemblies and no bundled reference assemblies were found."
        );
    }
}
