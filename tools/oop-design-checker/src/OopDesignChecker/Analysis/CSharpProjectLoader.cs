using System.Xml.Linq;
using Microsoft.Build.Locator;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.MSBuild;

namespace OopDesignChecker.Analysis;

internal sealed class CSharpプロジェクト読み込み器 : Iプロジェクト読み込み器
{
    private static readonly object MSBuild登録ロック = new();
    private readonly IReadOnlyList<string> _無視対象パス群;

    public CSharpプロジェクト読み込み器(IReadOnlyList<string>? 無視対象パス群 = null)
    {
        _無視対象パス群 = 無視対象パス群 ?? [];
    }

    public 解析対象プロジェクト 読み込む(
        string 対象パス,
        CancellationToken cancellationToken = default
    )
    {
        var projects = プロジェクト群を読み込む(対象パス, cancellationToken);
        if (projects.Count != 1)
        {
            throw new InvalidOperationException(
                $"Target contains {projects.Count} C# projects. Use project-set analysis for multi-project targets."
            );
        }

        return projects[0];
    }

    public IReadOnlyList<解析対象プロジェクト> プロジェクト群を読み込む(
        string 対象パス,
        CancellationToken cancellationToken = default
    )
    {
        cancellationToken.ThrowIfCancellationRequested();
        var fullTargetPath = Path.GetFullPath(対象パス);

        if (File.Exists(fullTargetPath))
        {
            return Path.GetExtension(fullTargetPath).ToLowerInvariant() switch
            {
                ".cs" => [個別ソース群を読み込む(fullTargetPath, cancellationToken)],
                ".csproj" => [MSBuildプロジェクトを読み込む(fullTargetPath, cancellationToken)],
                ".sln" => ソリューションを読み込む(fullTargetPath, cancellationToken),
                ".slnx" => Slnxプロジェクト群を読み込む(fullTargetPath, cancellationToken),
                _ => throw new InvalidOperationException(
                    "Target file must be a C# source file (.cs), project file (.csproj), or solution file (.sln/.slnx)."
                ),
            };
        }

        if (!Directory.Exists(fullTargetPath))
        {
            throw new InvalidOperationException($"Target does not exist: {fullTargetPath}");
        }

        var solutionFile = ソリューションファイルを探す(fullTargetPath, cancellationToken);
        if (solutionFile is not null)
        {
            return プロジェクト群を読み込む(solutionFile, cancellationToken);
        }

        var projectFiles = プロジェクトファイル群を探す(fullTargetPath, cancellationToken);
        return projectFiles.Length == 0
            ? [個別ソース群を読み込む(fullTargetPath, cancellationToken)]
            : MSBuildプロジェクト群を読み込む(projectFiles, cancellationToken);
    }

    private 解析対象プロジェクト[] ソリューションを読み込む(
        string solutionFile,
        CancellationToken cancellationToken
    )
    {
        cancellationToken.ThrowIfCancellationRequested();
        MSBuildを登録する();

        var workspaceFailures = new List<string>();
        using var workspace = MSBuildWorkspace.Create();
        using var workspaceFailureRegistration = workspace失敗処理を登録する(
            workspace,
            workspaceFailures
        );

        var solution = workspace
            .OpenSolutionAsync(solutionFile, cancellationToken: cancellationToken)
            .GetAwaiter()
            .GetResult();
        cancellationToken.ThrowIfCancellationRequested();
        workspace失敗を例外にする(solutionFile, workspaceFailures);

        var rootPath =
            Path.GetDirectoryName(solutionFile)
            ?? throw new InvalidOperationException(
                $"Could not determine solution directory for {solutionFile}."
            );
        var projects = solution
            .Projects.Where(project => project.Language == LanguageNames.CSharp)
            .Where(project =>
                string.IsNullOrWhiteSpace(project.FilePath)
                || !PathFilter.ShouldIgnore(project.FilePath, rootPath, _無視対象パス群)
            )
            .OrderBy(project => project.FilePath, パス比較規則.文字列比較子)
            .Select(project =>
                解析対象プロジェクトを作る(project, workspaceFailures, cancellationToken)
            )
            .ToArray();

        if (projects.Length == 0)
        {
            throw new InvalidOperationException(
                $"No analyzable C# projects were found in solution: {solutionFile}"
            );
        }

        return projects;
    }

    private 解析対象プロジェクト[] Slnxプロジェクト群を読み込む(
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
            .Select(path => Slnxプロジェクトパスを解決する(rootPath, path!))
            .Where(path =>
                string.Equals(Path.GetExtension(path), ".csproj", パス比較規則.文字列比較方式)
            )
            .Where(path => !PathFilter.ShouldIgnore(path, rootPath, _無視対象パス群))
            .Distinct(パス比較規則.文字列比較子)
            .OrderBy(path => path, パス比較規則.文字列比較子)
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

        return MSBuildプロジェクト群を読み込む(projectFiles, cancellationToken);
    }

    private 解析対象プロジェクト MSBuildプロジェクトを読み込む(
        string projectFile,
        CancellationToken cancellationToken
    )
    {
        cancellationToken.ThrowIfCancellationRequested();
        MSBuildを登録する();

        var workspaceFailures = new List<string>();
        using var workspace = MSBuildWorkspace.Create();
        using var workspaceFailureRegistration = workspace失敗処理を登録する(
            workspace,
            workspaceFailures
        );

        var project = workspace
            .OpenProjectAsync(projectFile, cancellationToken: cancellationToken)
            .GetAwaiter()
            .GetResult();
        return 解析対象プロジェクトを作る(project, workspaceFailures, cancellationToken);
    }

    private 解析対象プロジェクト[] MSBuildプロジェクト群を読み込む(
        string[] projectFiles,
        CancellationToken cancellationToken
    )
    {
        cancellationToken.ThrowIfCancellationRequested();
        MSBuildを登録する();

        var workspaceFailures = new List<string>();
        using var workspace = MSBuildWorkspace.Create();
        using var workspaceFailureRegistration = workspace失敗処理を登録する(
            workspace,
            workspaceFailures
        );

        var projects = new List<解析対象プロジェクト>(projectFiles.Length);
        foreach (var projectFile in projectFiles.OrderBy(path => path, パス比較規則.文字列比較子))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var normalizedProjectFile = Path.GetFullPath(projectFile);
            var project = workspace.CurrentSolution.Projects.FirstOrDefault(candidate =>
                string.Equals(
                    candidate.FilePath,
                    normalizedProjectFile,
                    パス比較規則.文字列比較方式
                )
            );
            project ??= workspace
                .OpenProjectAsync(normalizedProjectFile, cancellationToken: cancellationToken)
                .GetAwaiter()
                .GetResult();
            projects.Add(解析対象プロジェクトを作る(project, workspaceFailures, cancellationToken));
        }

        return projects.ToArray();
    }

    private 解析対象プロジェクト 解析対象プロジェクトを作る(
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

        コンパイル結果を検証する(compilation, workspaceFailures, cancellationToken);

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
                || !PathFilter.ShouldIgnore(tree.FilePath, rootPath, _無視対象パス群)
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

        return new 解析対象プロジェクト(
            rootPath,
            isApplication,
            compilation,
            semanticModels,
            syntaxTrees
        );
    }

    private 解析対象プロジェクト 個別ソース群を読み込む(
        string 対象パス,
        CancellationToken cancellationToken
    )
    {
        cancellationToken.ThrowIfCancellationRequested();
        var rootPath = File.Exists(対象パス)
            ? Path.GetDirectoryName(対象パス) ?? Directory.GetCurrentDirectory()
            : 対象パス;

        var sourceFiles = 個別ソースファイル群を探す(対象パス, rootPath, cancellationToken)
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
            references: メタデータ参照提供器.プラットフォーム参照群を作る(),
            options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary)
        );

        コンパイル結果を検証する(compilation, [], cancellationToken);

        var semanticModels = syntaxTrees.ToDictionary(
            tree => (SyntaxTree)tree,
            tree => compilation.GetSemanticModel(tree, ignoreAccessibility: true)
        );

        return new 解析対象プロジェクト(
            rootPath,
            プロジェクト種別判定器.アプリケーションか(rootPath),
            compilation,
            semanticModels,
            syntaxTrees
        );
    }

    private string? ソリューションファイルを探す(
        string targetDirectory,
        CancellationToken cancellationToken
    )
    {
        cancellationToken.ThrowIfCancellationRequested();
        var solutionFiles = Directory
            .EnumerateFiles(targetDirectory, "*.sln", SearchOption.TopDirectoryOnly)
            .Concat(
                Directory.EnumerateFiles(targetDirectory, "*.slnx", SearchOption.TopDirectoryOnly)
            )
            .Where(path => !PathFilter.ShouldIgnore(path, targetDirectory, _無視対象パス群))
            .OrderBy(path => path, パス比較規則.文字列比較子)
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

    private string[] プロジェクトファイル群を探す(
        string targetDirectory,
        CancellationToken cancellationToken
    )
    {
        cancellationToken.ThrowIfCancellationRequested();
        var directProjects = Directory
            .EnumerateFiles(targetDirectory, "*.csproj", SearchOption.TopDirectoryOnly)
            .Where(path => !PathFilter.ShouldIgnore(path, targetDirectory, _無視対象パス群))
            .OrderBy(path => path, パス比較規則.文字列比較子)
            .ToArray();

        if (directProjects.Length > 0)
        {
            return directProjects;
        }

        cancellationToken.ThrowIfCancellationRequested();
        return Directory
            .EnumerateFiles(targetDirectory, "*.csproj", SearchOption.AllDirectories)
            .Where(path => !PathFilter.ShouldIgnore(path, targetDirectory, _無視対象パス群))
            .Distinct(パス比較規則.文字列比較子)
            .OrderBy(path => path, パス比較規則.文字列比較子)
            .ToArray();
    }

    private IEnumerable<string> 個別ソースファイル群を探す(
        string 対象パス,
        string rootPath,
        CancellationToken cancellationToken
    )
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (File.Exists(対象パス))
        {
            if (
                string.Equals(Path.GetExtension(対象パス), ".cs", パス比較規則.文字列比較方式)
                && !PathFilter.ShouldIgnore(対象パス, rootPath, _無視対象パス群)
            )
            {
                yield return 対象パス;
            }

            yield break;
        }

        foreach (
            var file in Directory.EnumerateFiles(対象パス, "*.cs", SearchOption.AllDirectories)
        )
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!PathFilter.ShouldIgnore(file, rootPath, _無視対象パス群))
            {
                yield return file;
            }
        }
    }

    private static WorkspaceEventRegistration workspace失敗処理を登録する(
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

    private static string Slnxプロジェクトパスを解決する(string rootPath, string projectPath)
    {
        var normalizedPath = projectPath
            .Replace('\\', Path.DirectorySeparatorChar)
            .Replace('/', Path.DirectorySeparatorChar);
        return Path.GetFullPath(Path.Combine(rootPath, normalizedPath));
    }

    private static void workspace失敗を例外にする(string 対象パス, List<string> workspaceFailures)
    {
        if (workspaceFailures.Count == 0)
        {
            return;
        }

        throw new InvalidOperationException(
            $"Target {対象パス} could not be analyzed reliably because project loading contains errors:\n"
                + string.Join("\n", workspaceFailures.Select(message => $"MSBuild: {message}"))
        );
    }

    private static void コンパイル結果を検証する(
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

    private static void MSBuildを登録する()
    {
        if (MSBuildLocator.IsRegistered)
        {
            return;
        }

        lock (MSBuild登録ロック)
        {
            if (!MSBuildLocator.IsRegistered)
            {
                MSBuildLocator.RegisterDefaults();
            }
        }
    }
}
