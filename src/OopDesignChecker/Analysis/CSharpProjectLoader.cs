using System.Xml.Linq;
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
        var projects = LoadProjects(targetPath);
        if (projects.Count != 1)
        {
            throw new InvalidOperationException(
                $"Target contains {projects.Count} C# projects. Use project-set analysis for multi-project targets."
            );
        }

        return projects[0];
    }

    public IReadOnlyList<SourceProject> LoadProjects(string targetPath)
    {
        var fullTargetPath = Path.GetFullPath(targetPath);

        if (File.Exists(fullTargetPath))
        {
            return Path.GetExtension(fullTargetPath).ToLowerInvariant() switch
            {
                ".cs" => [LoadLooseSources(fullTargetPath)],
                ".csproj" => [LoadMsBuildProject(fullTargetPath)],
                ".sln" => LoadMsBuildSolution(fullTargetPath),
                ".slnx" => LoadSlnxProjects(fullTargetPath),
                _ => throw new InvalidOperationException(
                    "Target file must be a C# source file (.cs), project file (.csproj), or solution file (.sln/.slnx)."
                ),
            };
        }

        if (!Directory.Exists(fullTargetPath))
        {
            throw new InvalidOperationException($"Target does not exist: {fullTargetPath}");
        }

        var solutionFile = ResolveSolutionFile(fullTargetPath);
        if (solutionFile is not null)
        {
            return LoadProjects(solutionFile);
        }

        var projectFiles = ResolveProjectFiles(fullTargetPath);
        return projectFiles.Length == 0
            ? [LoadLooseSources(fullTargetPath)]
            : LoadMsBuildProjects(projectFiles);
    }

    private SourceProject[] LoadMsBuildSolution(string solutionFile)
    {
        EnsureMsBuildRegistered();

        var workspaceFailures = new List<string>();
        using var workspace = MSBuildWorkspace.Create();
        using var workspaceFailureRegistration = RegisterWorkspaceFailureHandler(
            workspace,
            workspaceFailures
        );

        var solution = workspace.OpenSolutionAsync(solutionFile).GetAwaiter().GetResult();
        ThrowIfWorkspaceFailures(solutionFile, workspaceFailures);

        var rootPath =
            Path.GetDirectoryName(solutionFile)
            ?? throw new InvalidOperationException(
                $"Could not determine solution directory for {solutionFile}."
            );
        var projects = solution
            .Projects.Where(project => project.Language == LanguageNames.CSharp)
            .Where(project =>
                string.IsNullOrWhiteSpace(project.FilePath)
                || !PathFilter.ShouldIgnore(project.FilePath, rootPath, _ignoredPaths)
            )
            .OrderBy(project => project.FilePath, StringComparer.OrdinalIgnoreCase)
            .Select(project => CreateSourceProject(project, workspaceFailures))
            .ToArray();

        if (projects.Length == 0)
        {
            throw new InvalidOperationException(
                $"No analyzable C# projects were found in solution: {solutionFile}"
            );
        }

        return projects;
    }

    private SourceProject[] LoadSlnxProjects(string solutionFile)
    {
        var rootPath =
            Path.GetDirectoryName(solutionFile)
            ?? throw new InvalidOperationException(
                $"Could not determine solution directory for {solutionFile}."
            );
        var document = XDocument.Load(solutionFile, LoadOptions.None);
        var projectFiles = document
            .Descendants()
            .Where(element => element.Name.LocalName == "Project")
            .Select(element => element.Attribute("Path")?.Value)
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .Select(path => ResolveSlnxProjectPath(rootPath, path!))
            .Where(path =>
                string.Equals(
                    Path.GetExtension(path),
                    ".csproj",
                    StringComparison.OrdinalIgnoreCase
                )
            )
            .Where(path => !PathFilter.ShouldIgnore(path, rootPath, _ignoredPaths))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (projectFiles.Length == 0)
        {
            throw new InvalidOperationException(
                $"No analyzable C# projects were found in solution: {solutionFile}"
            );
        }

        var missingProjects = projectFiles.Where(path => !File.Exists(path)).ToArray();
        if (missingProjects.Length > 0)
        {
            throw new InvalidOperationException(
                "Solution references missing C# project files:\n"
                    + string.Join("\n", missingProjects)
            );
        }

        return LoadMsBuildProjects(projectFiles);
    }

    private SourceProject LoadMsBuildProject(string projectFile)
    {
        EnsureMsBuildRegistered();

        var workspaceFailures = new List<string>();
        using var workspace = MSBuildWorkspace.Create();
        using var workspaceFailureRegistration = RegisterWorkspaceFailureHandler(
            workspace,
            workspaceFailures
        );

        var project = workspace.OpenProjectAsync(projectFile).GetAwaiter().GetResult();
        return CreateSourceProject(project, workspaceFailures);
    }

    private SourceProject[] LoadMsBuildProjects(IReadOnlyCollection<string> projectFiles) =>
        projectFiles
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .Select(LoadMsBuildProject)
            .ToArray();

    private SourceProject CreateSourceProject(Project project, List<string> workspaceFailures)
    {
        if (
            project.GetCompilationAsync().GetAwaiter().GetResult()
            is not CSharpCompilation compilation
        )
        {
            throw new InvalidOperationException(
                $"Could not create a C# compilation for {project.FilePath ?? project.Name}."
            );
        }

        ValidateCompilation(compilation, workspaceFailures);

        var projectFile =
            project.FilePath
            ?? throw new InvalidOperationException(
                $"Could not determine project file path for {project.Name}."
            );
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

        return new SourceProject(rootPath, isApplication, compilation, semanticModels, syntaxTrees);
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

    private string? ResolveSolutionFile(string targetDirectory)
    {
        var solutionFiles = Directory
            .EnumerateFiles(targetDirectory, "*.sln", SearchOption.TopDirectoryOnly)
            .Concat(
                Directory.EnumerateFiles(targetDirectory, "*.slnx", SearchOption.TopDirectoryOnly)
            )
            .Where(path => !PathFilter.ShouldIgnore(path, targetDirectory, _ignoredPaths))
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return solutionFiles.Length switch
        {
            0 => null,
            1 => solutionFiles[0],
            _ => throw new InvalidOperationException(
                $"Multiple solution files were found under {targetDirectory}. Target a specific .sln or .slnx file."
            ),
        };
    }

    private string[] ResolveProjectFiles(string targetDirectory)
    {
        var directProjects = Directory
            .EnumerateFiles(targetDirectory, "*.csproj", SearchOption.TopDirectoryOnly)
            .Where(path => !PathFilter.ShouldIgnore(path, targetDirectory, _ignoredPaths))
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (directProjects.Length > 0)
        {
            return directProjects;
        }

        return Directory
            .EnumerateFiles(targetDirectory, "*.csproj", SearchOption.AllDirectories)
            .Where(path => !PathFilter.ShouldIgnore(path, targetDirectory, _ignoredPaths))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

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

    private static WorkspaceEventRegistration RegisterWorkspaceFailureHandler(
        MSBuildWorkspace workspace,
        List<string> workspaceFailures
    ) =>
        workspace.RegisterWorkspaceFailedHandler(args =>
        {
            if (args.Diagnostic.Kind == WorkspaceDiagnosticKind.Failure)
            {
                workspaceFailures.Add(args.Diagnostic.Message);
            }
        });

    private static string ResolveSlnxProjectPath(string rootPath, string projectPath)
    {
        var normalizedPath = projectPath
            .Replace('\\', Path.DirectorySeparatorChar)
            .Replace('/', Path.DirectorySeparatorChar);
        return Path.GetFullPath(Path.Combine(rootPath, normalizedPath));
    }

    private static void ThrowIfWorkspaceFailures(string targetPath, List<string> workspaceFailures)
    {
        if (workspaceFailures.Count == 0)
        {
            return;
        }

        throw new InvalidOperationException(
            $"Target {targetPath} could not be analyzed reliably because project loading contains errors:\n"
                + string.Join("\n", workspaceFailures.Select(message => $"MSBuild: {message}"))
        );
    }

    private static void ValidateCompilation(
        CSharpCompilation compilation,
        List<string> workspaceFailures
    )
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
