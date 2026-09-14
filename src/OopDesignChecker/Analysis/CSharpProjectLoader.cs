using Microsoft.Build.Locator;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.MSBuild;

namespace OopDesignChecker.Analysis;

internal sealed class CSharpProjectLoader : IProjectLoader
{
    private static readonly object MsBuildRegistrationLock = new();
    private readonly IReadOnlyList<string> _ignoredPaths;

    public CSharpProjectLoader(IReadOnlyList<string>? ignoredPaths = null)
    {
        _ignoredPaths = ignoredPaths ?? [];
    }

    public SourceProject Load(string targetPath)
    {
        var fullTargetPath = Path.GetFullPath(targetPath);

        if (File.Exists(fullTargetPath))
        {
            return Path.GetExtension(fullTargetPath).ToLowerInvariant() switch
            {
                ".cs" => LoadLooseSources(fullTargetPath),
                ".csproj" => LoadMsBuildProject(fullTargetPath),
                ".sln" or ".slnx" => throw new InvalidOperationException(
                    "Solution-wide analysis is not implemented yet. Target a single .csproj instead."
                ),
                _ => throw new InvalidOperationException(
                    "Target file must be a C# source file (.cs) or project file (.csproj)."
                ),
            };
        }

        if (!Directory.Exists(fullTargetPath))
        {
            throw new InvalidOperationException($"Target does not exist: {fullTargetPath}");
        }

        var projectFile = ResolveProjectFile(fullTargetPath);
        return projectFile is null
            ? LoadLooseSources(fullTargetPath)
            : LoadMsBuildProject(projectFile);
    }

    private SourceProject LoadMsBuildProject(string projectFile)
    {
        EnsureMsBuildRegistered();

        var workspaceFailures = new List<string>();
        using var workspace = MSBuildWorkspace.Create();
        using var workspaceFailureRegistration = workspace.RegisterWorkspaceFailedHandler(args =>
        {
            if (args.Diagnostic.Kind == WorkspaceDiagnosticKind.Failure)
            {
                workspaceFailures.Add(args.Diagnostic.Message);
            }
        });

        var project = workspace.OpenProjectAsync(projectFile).GetAwaiter().GetResult();
        if (
            project.GetCompilationAsync().GetAwaiter().GetResult()
            is not CSharpCompilation compilation
        )
        {
            throw new InvalidOperationException(
                $"Could not create a C# compilation for {projectFile}."
            );
        }

        ValidateCompilation(compilation, workspaceFailures);

        var rootPath =
            Path.GetDirectoryName(projectFile)
            ?? throw new InvalidOperationException(
                $"Could not determine project directory for {projectFile}."
            );
        var syntaxTrees = project
            .Documents.Select(document => document.GetSyntaxTreeAsync().GetAwaiter().GetResult())
            .Where(tree => tree is not null)
            .Cast<SyntaxTree>()
            .Where(tree =>
                string.IsNullOrWhiteSpace(tree.FilePath)
                || !PathFilter.ShouldIgnore(tree.FilePath, rootPath, _ignoredPaths)
            )
            .Distinct()
            .ToArray();
        var semanticModels = syntaxTrees.ToDictionary(
            tree => tree,
            tree => compilation.GetSemanticModel(tree, ignoreAccessibility: true)
        );

        var isApplication =
            compilation.Options.OutputKind
            is OutputKind.ConsoleApplication
                or OutputKind.WindowsApplication
                or OutputKind.WindowsRuntimeApplication;

        return new SourceProject(
            rootPath,
            isApplication,
            compilation,
            semanticModels,
            syntaxTrees
        );
    }

    private SourceProject LoadLooseSources(string targetPath)
    {
        var rootPath = File.Exists(targetPath)
            ? Path.GetDirectoryName(targetPath) ?? Directory.GetCurrentDirectory()
            : targetPath;

        var sourceFiles = DiscoverLooseSourceFiles(targetPath, rootPath).ToArray();
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

        ValidateCompilation(compilation, []);

        var semanticModels = syntaxTrees.ToDictionary(
            tree => (SyntaxTree)tree,
            tree => compilation.GetSemanticModel(tree, ignoreAccessibility: true)
        );

        return new SourceProject(
            rootPath,
            ProjectTypeDetector.IsApplication(rootPath),
            compilation,
            semanticModels,
            syntaxTrees
        );
    }

    private string? ResolveProjectFile(string targetDirectory)
    {
        var directProjects = Directory
            .EnumerateFiles(targetDirectory, "*.csproj", SearchOption.TopDirectoryOnly)
            .Where(path => !PathFilter.ShouldIgnore(path, targetDirectory, _ignoredPaths))
            .ToArray();

        if (directProjects.Length == 1)
        {
            return directProjects[0];
        }

        if (directProjects.Length > 1)
        {
            throw MultipleProjectsFound(targetDirectory, directProjects);
        }

        var recursiveProjects = Directory
            .EnumerateFiles(targetDirectory, "*.csproj", SearchOption.AllDirectories)
            .Where(path => !PathFilter.ShouldIgnore(path, targetDirectory, _ignoredPaths))
            .ToArray();

        return recursiveProjects.Length switch
        {
            0 => null,
            1 => recursiveProjects[0],
            _ => throw MultipleProjectsFound(targetDirectory, recursiveProjects),
        };
    }

    private static InvalidOperationException MultipleProjectsFound(
        string targetDirectory,
        IReadOnlyCollection<string> projectFiles
    ) =>
        new(
            $"Multiple C# projects were found under {targetDirectory}. "
                + "Target a single .csproj to avoid mixing unrelated project compilations. "
                + $"Found: {string.Join(", ", projectFiles.Select(Path.GetFileName))}"
        );

    private IEnumerable<string> DiscoverLooseSourceFiles(string targetPath, string rootPath)
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

    private static void ValidateCompilation(CSharpCompilation compilation, List<string> workspaceFailures)
    {
        var errors = compilation
            .GetDiagnostics()
            .Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error)
            .Take(20)
            .ToArray();

        if (errors.Length == 0 && workspaceFailures.Count == 0)
        {
            return;
        }

        var details = errors.Select(error => error.ToString()).ToList();
        details.AddRange(workspaceFailures.Select(message => $"MSBuild: {message}"));

        throw new InvalidOperationException(
            "Target project could not be analyzed reliably because its compilation or project loading contains errors:\n"
                + string.Join("\n", details)
        );
    }

    private static void EnsureMsBuildRegistered()
    {
        if (MSBuildLocator.IsRegistered)
        {
            return;
        }

        lock (MsBuildRegistrationLock)
        {
            if (!MSBuildLocator.IsRegistered)
            {
                MSBuildLocator.RegisterDefaults();
            }
        }
    }
}
