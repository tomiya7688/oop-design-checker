using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace OopDesignChecker.Analysis;

internal sealed class CSharpProjectLoader : IProjectLoader
{
    private readonly IReadOnlyList<string> _ignoredPaths;

    public CSharpProjectLoader(IReadOnlyList<string>? ignoredPaths = null)
    {
        _ignoredPaths = ignoredPaths ?? [];
    }

    public SourceProject Load(string targetPath)
    {
        var rootPath = File.Exists(targetPath)
            ? Path.GetDirectoryName(targetPath) ?? Directory.GetCurrentDirectory()
            : targetPath;

        var sourceFiles = DiscoverSourceFiles(targetPath, rootPath).ToArray();
        if (sourceFiles.Length == 0)
        {
            throw new InvalidOperationException("No C# source files were found.");
        }

        var parseOptions = CSharpParseOptions.Default.WithLanguageVersion(LanguageVersion.Preview);
        var syntaxTrees = sourceFiles
            .Select(path => CSharpSyntaxTree.ParseText(File.ReadAllText(path), parseOptions, path))
            .ToArray();

        var compilation = CSharpCompilation.Create(
            assemblyName: "OopDesignChecker.Target",
            syntaxTrees: syntaxTrees,
            references: MetadataReferenceProvider.CreatePlatformReferences(),
            options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary)
        );

        var semanticModels = syntaxTrees.ToDictionary(
            tree => (SyntaxTree)tree,
            tree => compilation.GetSemanticModel(tree, ignoreAccessibility: true)
        );

        return new SourceProject(
            rootPath,
            ProjectTypeDetector.IsApplication(rootPath),
            compilation,
            semanticModels
        );
    }

    private IEnumerable<string> DiscoverSourceFiles(string targetPath, string rootPath)
    {
        if (File.Exists(targetPath))
        {
            if (
                string.Equals(
                    Path.GetExtension(targetPath),
                    ".cs",
                    StringComparison.OrdinalIgnoreCase
                ) && !PathFilter.ShouldIgnore(targetPath, rootPath, _ignoredPaths)
            )
            {
                yield return targetPath;
            }

            yield break;
        }

        foreach (
            var file in Directory.EnumerateFiles(targetPath, "*.cs", SearchOption.AllDirectories)
        )
        {
            if (!PathFilter.ShouldIgnore(file, rootPath, _ignoredPaths))
            {
                yield return file;
            }
        }
    }
}
