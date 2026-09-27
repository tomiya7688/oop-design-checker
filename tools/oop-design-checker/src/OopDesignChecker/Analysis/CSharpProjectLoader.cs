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

    public SourceProject Load(string targetPath, CancellationToken cancellationToken = default)
    {
        var projects = LoadProjects(targetPath, cancellationToken);
        if (projects.Count != 1)
        {
            throw new InvalidOperationException(
                $"Target contains {projects.Count} C# projects. Use project-set analysis for multi-project targets."
            );
        }

        return projects[0];
    }

    public IReadOnlyList<SourceProject> LoadProjects(
        string targetPath,
        CancellationToken cancellationToken = default
    )
    {
        cancellationToken.ThrowIfCancellationRequested();
        var fullTargetPath = Path.GetFullPath(targetPath);

        if (File.Exists(fullTargetPath))
        {
            return Path.GetExtension(fullTargetPath).ToLowerInvariant() switch
            {
                ".cs" => [LoadLooseSources(fullTargetPath, cancellationToken)],
                ".csproj" => [LoadMsBuildProject(fullTargetPath, cancellationToken)],
                ".sln" => LoadMsBuildSolution(fullTargetPath, cancellationToken),
                ".slnx" => LoadSlnxProjects(fullTargetPath, cancellationToken),
                _ => throw new InvalidOperationException(
                    "Target file must be a C# source file (.cs), project file (.csproj), or solution file (.sln/.slnx)."
                ),
            };
        }

        if (!Directory.Exists(fullTargetPath))
        {
            throw new InvalidOperationException($"Target does not exist: {fullTargetPath}");
        }

        var solutionFile = ResolveSolutionFile(fullTargetPath, cancellationToken);
        if (solutionFile is not null)
        {
            return LoadProjects(solutionFile, cancellationToken);
        }

        var projectFiles = ResolveProjectFiles(fullTargetPath, cancellationToken);
        return projectFiles.Length == 0
            ? [LoadLooseSources(fullTargetPath, cancellationToken)]
            : LoadMsBuildProjects(projectFiles, cancellationToken);
    }

    private SourceProject[] LoadMsBuildSolution(
        string solutionFile,
        CancellationToken cancellationToken
    )
    {
        cancellationToken.ThrowIfCancellationRequested();
        EnsureMsBuildRegistered();

        var workspaceFailures = new List<string>();
        using var workspace = MSBuildWorkspace.Create();
        using var workspaceFailureRegistration = RegisterWorkspaceFailureHandler(
            workspace,
            workspaceFailures
        );

        var solution = workspace
            .OpenSolutionAsync(solutionFile, cancellationToken: cancellationToken)
            .GetAwaiter()
            .GetResult();
        cancellationToken.ThrowIfCancellationRequested();
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
            .OrderBy(project => project.FilePath, PathSemantics.Comparer)
            .Select(project => CreateSourceProject(project, workspaceFailures, cancellationToken))
            .ToArray();

        if (projects.Length == 0)
        {
            throw new InvalidOperationException(
                $"No analyzable C# projects were found in solution: {solutionFile}"
            );
        }

        return projects;
    }

    private SourceProject[] LoadSlnxProjects(
        string solutionFile,
        CancellationToken cancellationToken
    )
    {
        cancellationToken.ThrowIfCancellationRequested();
        var rootPath =
            Path.GetDirectoryName(solutionFile)
            ?? throw new InvalidOperationException(
                $"Could not determine solution directory for {solutionFile}."
            );
        var document = XDocument.Load(solutionFile, LoadOptions.None);
        cancellationToken.ThrowIfCancellationRequested();
        var projectFiles = document
            .Descendants()
            .Where(element => element.Name.LocalName == "Project")
            .Select(element => element.Attribute("Path")?.Value)
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .Select(path => ResolveSlnxProjectPath(rootPath, path!))
            .Where(path =>
                string.Equals(Path.GetExtension(path), ".csproj", PathSemantics.Comparison)
            )
            .Where(path => !PathFilter.ShouldIgnore(path, rootPath, _ignoredPaths))
            .Distinct(PathSemantics.Comparer)
            .OrderBy(path => path, PathSemantics.Comparer)
            .ToArray();

        if (projectFiles.Length == 0)
        {
            throw new InvalidOperationException(
                $"No analyzable C# projects were found in solution: {solutionFile}"
            );
        }

        cancellationToken.ThrowIfCancellationRequested();
        var missingProjects = projectFiles.Where(path => !File.Exists(path)).ToArray();
        if (missingProjects.Length > 0)
        {
            throw new InvalidOperationException(
                "Solution references missing C# project files:\n"
                    + string.Join("\n", missingProjects)
            );
        }

        return LoadMsBuildProjects(projectFiles, cancellationToken);
    }

    private SourceProject LoadMsBuildProject(
        string projectFile,
        CancellationToken cancellationToken
    )
    {
        cancellationToken.ThrowIfCancellationRequested();
        EnsureMsBuildRegistered();

        var workspaceFailures = new List<string>();
        using var workspace = MSBuildWorkspace.Create();
        using var workspaceFailureRegistration = RegisterWorkspaceFailureHandler(
            workspace,
            workspaceFailures
        );

        var project = workspace
            .OpenProjectAsync(projectFile, cancellationToken: cancellationToken)
            .GetAwaiter()
            .GetResult();
        return CreateSourceProject(project, workspaceFailures, cancellationToken);
    }

    private SourceProject[] LoadMsBuildProjects(
        string[] projectFiles,
        CancellationToken cancellationToken
    )
    {
        cancellationToken.ThrowIfCancellationRequested();
        EnsureMsBuildRegistered();

        var workspaceFailures = new List<string>();
        using var workspace = MSBuildWorkspace.Create();
        using var workspaceFailureRegistration = RegisterWorkspaceFailureHandler(
            workspace,
            workspaceFailures
        );

        var projects = new List<SourceProject>(projectFiles.Length);
        foreach (var projectFile in projectFiles.OrderBy(path => path, PathSemantics.Comparer))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var normalizedProjectFile = Path.GetFullPath(projectFile);
            var project = workspace.CurrentSolution.Projects.FirstOrDefault(candidate =>
                string.Equals(candidate.FilePath, normalizedProjectFile, PathSemantics.Comparison)
            );
            project ??= workspace
                .OpenProjectAsync(normalizedProjectFile, cancellationToken: cancellationToken)
                .GetAwaiter()
                .GetResult();
            projects.Add(CreateSourceProject(project, workspaceFailures, cancellationToken));
        }

        return projects.ToArray();
    }

    private SourceProject CreateSourceProject(
        Project project,
        List<string> workspaceFailures,
        CancellationToken cancellationToken
    )
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (
            project.GetCompilationAsync(cancellationToken).GetAwaiter().GetResult()
            is not CSharpCompilation compilation
        )
        {
            throw new InvalidOperationException(
                $"Could not create a C# compilation for {project.FilePath ?? project.Name}."
            );
        }

        ValidateCompilation(compilation, workspaceFailures, cancellationToken);

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
            .Documents.Select(document =>
                document.GetSyntaxTreeAsync(cancellationToken).GetAwaiter().GetResult()
            )
            .Where(tree => tree is not null)
            .Cast<SyntaxTree>()
            .Where(tree =>
                string.IsNullOrWhiteSpace(tree.FilePath)
                || !PathFilter.ShouldIgnore(tree.FilePath, rootPath, _ignoredPaths)
            )
            .Distinct()
            .ToArray();
        cancellationToken.ThrowIfCancellationRequested();
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

    private SourceProject LoadLooseSources(string targetPath, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var rootPath = File.Exists(targetPath)
            ? Path.GetDirectoryName(targetPath) ?? Directory.GetCurrentDirectory()
            : targetPath;

        var sourceFiles = DiscoverLooseSourceFiles(targetPath, rootPath, cancellationToken)
            .ToArray();
        if (sourceFiles.Length == 0)
        {
            throw new InvalidOperationException("No C# source files were found.");
        }

        var parseOptions = CSharpParseOptions.Default.WithLanguageVersion(LanguageVersion.Preview);
        var syntaxTrees = sourceFiles
            .Select(path =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                return CSharpSyntaxTree.ParseText(
                    File.ReadAllText(path),
                    parseOptions,
                    path,
                    cancellationToken: cancellationToken
                );
            })
            .ToArray();

        var compilation = CSharpCompilation.Create(
            assemblyName: "OopDesignChecker.Target",
            syntaxTrees: syntaxTrees,
            references: MetadataReferenceProvider.CreatePlatformReferences(),
            options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary)
        );

        ValidateCompilation(compilation, [], cancellationToken);

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

    private string? ResolveSolutionFile(string targetDirectory, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var solutionFiles = Directory
            .EnumerateFiles(targetDirectory, "*.sln", SearchOption.TopDirectoryOnly)
            .Concat(
                Directory.EnumerateFiles(targetDirectory, "*.slnx", SearchOption.TopDirectoryOnly)
            )
            .Where(path => !PathFilter.ShouldIgnore(path, targetDirectory, _ignoredPaths))
            .OrderBy(path => path, PathSemantics.Comparer)
            .ToArray();

        cancellationToken.ThrowIfCancellationRequested();
        return solutionFiles.Length switch
        {
            0 => null,
            1 => solutionFiles[0],
            _ => throw new InvalidOperationException(
                $"Multiple solution files were found under {targetDirectory}. Target a specific .sln or .slnx file."
            ),
        };
    }

    private string[] ResolveProjectFiles(
        string targetDirectory,
        CancellationToken cancellationToken
    )
    {
        cancellationToken.ThrowIfCancellationRequested();
        var directProjects = Directory
            .EnumerateFiles(targetDirectory, "*.csproj", SearchOption.TopDirectoryOnly)
            .Where(path => !PathFilter.ShouldIgnore(path, targetDirectory, _ignoredPaths))
            .OrderBy(path => path, PathSemantics.Comparer)
            .ToArray();

        if (directProjects.Length > 0)
        {
            return directProjects;
        }

        cancellationToken.ThrowIfCancellationRequested();
        return Directory
            .EnumerateFiles(targetDirectory, "*.csproj", SearchOption.AllDirectories)
            .Where(path => !PathFilter.ShouldIgnore(path, targetDirectory, _ignoredPaths))
            .Distinct(PathSemantics.Comparer)
            .OrderBy(path => path, PathSemantics.Comparer)
            .ToArray();
    }

    private IEnumerable<string> DiscoverLooseSourceFiles(
        string targetPath,
        string rootPath,
        CancellationToken cancellationToken
    )
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (File.Exists(targetPath))
        {
            if (
                string.Equals(Path.GetExtension(targetPath), ".cs", PathSemantics.Comparison)
                && !PathFilter.ShouldIgnore(targetPath, rootPath, _ignoredPaths)
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
            cancellationToken.ThrowIfCancellationRequested();
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
        List<string> workspaceFailures,
        CancellationToken cancellationToken
    )
    {
        var errors = compilation
            .GetDiagnostics(cancellationToken)
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
