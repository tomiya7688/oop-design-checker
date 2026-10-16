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
        CancellationToken キャンセル通知 = default
    )
    {
        var projects = プロジェクト群を読み込む(対象パス, キャンセル通知);
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
        CancellationToken キャンセル通知 = default
    )
    {
        キャンセル通知.ThrowIfCancellationRequested();
        var fullTargetPath = Path.GetFullPath(対象パス);

        if (File.Exists(fullTargetPath))
        {
            return Path.GetExtension(fullTargetPath).ToLowerInvariant() switch
            {
                ".cs" => [個別ソース群を読み込む(fullTargetPath, キャンセル通知)],
                ".csproj" => [MSBuildプロジェクトを読み込む(fullTargetPath, キャンセル通知)],
                ".sln" => ソリューションを読み込む(fullTargetPath, キャンセル通知),
                ".slnx" => Slnxプロジェクト群を読み込む(fullTargetPath, キャンセル通知),
                _ => throw new InvalidOperationException(
                    "Target file must be a C# source file (.cs), project file (.csproj), or solution file (.sln/.slnx)."
                ),
            };
        }

        if (!Directory.Exists(fullTargetPath))
        {
            throw new InvalidOperationException($"Target does not exist: {fullTargetPath}");
        }

        var solutionFile = ソリューションファイルを探す(fullTargetPath, キャンセル通知);
        if (solutionFile is not null)
        {
            return プロジェクト群を読み込む(solutionFile, キャンセル通知);
        }

        var projectFiles = プロジェクトファイル群を探す(fullTargetPath, キャンセル通知);
        return projectFiles.Length == 0
            ? [個別ソース群を読み込む(fullTargetPath, キャンセル通知)]
            : MSBuildプロジェクト群を読み込む(projectFiles, キャンセル通知);
    }

    private 解析対象プロジェクト[] ソリューションを読み込む(
        string solutionFile,
        CancellationToken キャンセル通知
    )
    {
        キャンセル通知.ThrowIfCancellationRequested();
        MSBuildを登録する();

        var 作業領域失敗群 = new List<string>();
        using var workspace = MSBuildWorkspace.Create();
        using var 作業領域失敗登録 = 作業領域の失敗処理を登録する(workspace, 作業領域失敗群);

        var solution = workspace
            .OpenSolutionAsync(solutionFile, cancellationToken: キャンセル通知)
            .GetAwaiter()
            .GetResult();
        キャンセル通知.ThrowIfCancellationRequested();
        作業領域の失敗を例外にする(solutionFile, 作業領域失敗群);

        var rootPath =
            Path.GetDirectoryName(solutionFile)
            ?? throw new InvalidOperationException(
                $"Could not determine solution directory for {solutionFile}."
            );
        var projects = solution
            .Projects.Where(project => project.Language == LanguageNames.CSharp)
            .Where(project =>
                string.IsNullOrWhiteSpace(project.FilePath)
                || !パス除外判定器.無視する(project.FilePath, rootPath, _無視対象パス群)
            )
            .OrderBy(project => project.FilePath, パス比較規則.文字列比較子)
            .Select(project => 解析対象プロジェクトを作る(project, 作業領域失敗群, キャンセル通知))
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
        CancellationToken キャンセル通知
    )
    {
        キャンセル通知.ThrowIfCancellationRequested();
        var rootPath =
            Path.GetDirectoryName(solutionFile)
            ?? throw new InvalidOperationException(
                $"Could not determine solution directory for {solutionFile}."
            );
        var document = XDocument.Load(solutionFile, LoadOptions.None);
        キャンセル通知.ThrowIfCancellationRequested();
        var projectFiles = document
            .Descendants()
            .Where(element => element.Name.LocalName == "Project")
            .Select(element => element.Attribute("Path")?.Value)
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .Select(path => Slnxプロジェクトパスを解決する(rootPath, path!))
            .Where(path =>
                string.Equals(Path.GetExtension(path), ".csproj", パス比較規則.文字列比較方式)
            )
            .Where(path => !パス除外判定器.無視する(path, rootPath, _無視対象パス群))
            .Distinct(パス比較規則.文字列比較子)
            .OrderBy(path => path, パス比較規則.文字列比較子)
            .ToArray();

        if (projectFiles.Length == 0)
        {
            throw new InvalidOperationException(
                $"No analyzable C# projects were found in solution: {solutionFile}"
            );
        }

        キャンセル通知.ThrowIfCancellationRequested();
        var 存在しないプロジェクトファイル群 = projectFiles
            .Where(path => !File.Exists(path))
            .ToArray();
        if (存在しないプロジェクトファイル群.Length > 0)
        {
            throw new InvalidOperationException(
                "Solution references missing C# project files:\n"
                    + string.Join("\n", 存在しないプロジェクトファイル群)
            );
        }

        return MSBuildプロジェクト群を読み込む(projectFiles, キャンセル通知);
    }

    private 解析対象プロジェクト MSBuildプロジェクトを読み込む(
        string projectFile,
        CancellationToken キャンセル通知
    )
    {
        キャンセル通知.ThrowIfCancellationRequested();
        MSBuildを登録する();

        var 作業領域失敗群 = new List<string>();
        using var workspace = MSBuildWorkspace.Create();
        using var 作業領域失敗登録 = 作業領域の失敗処理を登録する(workspace, 作業領域失敗群);

        var project = workspace
            .OpenProjectAsync(projectFile, cancellationToken: キャンセル通知)
            .GetAwaiter()
            .GetResult();
        return 解析対象プロジェクトを作る(project, 作業領域失敗群, キャンセル通知);
    }

    private 解析対象プロジェクト[] MSBuildプロジェクト群を読み込む(
        string[] projectFiles,
        CancellationToken キャンセル通知
    )
    {
        キャンセル通知.ThrowIfCancellationRequested();
        MSBuildを登録する();

        var 作業領域失敗群 = new List<string>();
        using var workspace = MSBuildWorkspace.Create();
        using var 作業領域失敗登録 = 作業領域の失敗処理を登録する(workspace, 作業領域失敗群);

        var projects = new List<解析対象プロジェクト>(projectFiles.Length);
        foreach (var projectFile in projectFiles.OrderBy(path => path, パス比較規則.文字列比較子))
        {
            キャンセル通知.ThrowIfCancellationRequested();
            var normalizedProjectFile = Path.GetFullPath(projectFile);
            var project = workspace.CurrentSolution.Projects.FirstOrDefault(candidate =>
                string.Equals(
                    candidate.FilePath,
                    normalizedProjectFile,
                    パス比較規則.文字列比較方式
                )
            );
            project ??= workspace
                .OpenProjectAsync(normalizedProjectFile, cancellationToken: キャンセル通知)
                .GetAwaiter()
                .GetResult();
            projects.Add(解析対象プロジェクトを作る(project, 作業領域失敗群, キャンセル通知));
        }

        return projects.ToArray();
    }

    private 解析対象プロジェクト 解析対象プロジェクトを作る(
        Project project,
        List<string> 作業領域失敗群,
        CancellationToken キャンセル通知
    )
    {
        キャンセル通知.ThrowIfCancellationRequested();
        if (
            project.GetCompilationAsync(キャンセル通知).GetAwaiter().GetResult()
            is not CSharpCompilation compilation
        )
        {
            throw new InvalidOperationException(
                $"Could not create a C# compilation for {project.FilePath ?? project.Name}."
            );
        }

        コンパイル結果を検証する(compilation, 作業領域失敗群, キャンセル通知);

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
                document.GetSyntaxTreeAsync(キャンセル通知).GetAwaiter().GetResult()
            )
            .Where(tree => tree is not null)
            .Cast<SyntaxTree>()
            .Where(tree =>
                string.IsNullOrWhiteSpace(tree.FilePath)
                || !パス除外判定器.無視する(tree.FilePath, rootPath, _無視対象パス群)
            )
            .Distinct()
            .ToArray();
        キャンセル通知.ThrowIfCancellationRequested();
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
        CancellationToken キャンセル通知
    )
    {
        キャンセル通知.ThrowIfCancellationRequested();
        var rootPath = File.Exists(対象パス)
            ? Path.GetDirectoryName(対象パス) ?? Directory.GetCurrentDirectory()
            : 対象パス;

        var sourceFiles = 個別ソースファイル群を探す(対象パス, rootPath, キャンセル通知).ToArray();
        if (sourceFiles.Length == 0)
        {
            throw new InvalidOperationException("No C# source files were found.");
        }

        var parseOptions = CSharpParseOptions.Default.WithLanguageVersion(LanguageVersion.Preview);
        var syntaxTrees = sourceFiles
            .Select(path =>
            {
                キャンセル通知.ThrowIfCancellationRequested();
                return CSharpSyntaxTree.ParseText(
                    File.ReadAllText(path),
                    parseOptions,
                    path,
                    cancellationToken: キャンセル通知
                );
            })
            .ToArray();

        var compilation = CSharpCompilation.Create(
            assemblyName: "OopDesignChecker.Target",
            syntaxTrees: syntaxTrees,
            references: メタデータ参照提供器.プラットフォーム参照群を作る(),
            options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary)
        );

        コンパイル結果を検証する(compilation, [], キャンセル通知);

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
        CancellationToken キャンセル通知
    )
    {
        キャンセル通知.ThrowIfCancellationRequested();
        var solutionFiles = Directory
            .EnumerateFiles(targetDirectory, "*.sln", SearchOption.TopDirectoryOnly)
            .Concat(
                Directory.EnumerateFiles(targetDirectory, "*.slnx", SearchOption.TopDirectoryOnly)
            )
            .Where(path => !パス除外判定器.無視する(path, targetDirectory, _無視対象パス群))
            .OrderBy(path => path, パス比較規則.文字列比較子)
            .ToArray();

        キャンセル通知.ThrowIfCancellationRequested();
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
        CancellationToken キャンセル通知
    )
    {
        キャンセル通知.ThrowIfCancellationRequested();
        var 直下のプロジェクトファイル群 = Directory
            .EnumerateFiles(targetDirectory, "*.csproj", SearchOption.TopDirectoryOnly)
            .Where(path => !パス除外判定器.無視する(path, targetDirectory, _無視対象パス群))
            .OrderBy(path => path, パス比較規則.文字列比較子)
            .ToArray();

        if (直下のプロジェクトファイル群.Length > 0)
        {
            return 直下のプロジェクトファイル群;
        }

        キャンセル通知.ThrowIfCancellationRequested();
        return Directory
            .EnumerateFiles(targetDirectory, "*.csproj", SearchOption.AllDirectories)
            .Where(path => !パス除外判定器.無視する(path, targetDirectory, _無視対象パス群))
            .Distinct(パス比較規則.文字列比較子)
            .OrderBy(path => path, パス比較規則.文字列比較子)
            .ToArray();
    }

    private IEnumerable<string> 個別ソースファイル群を探す(
        string 対象パス,
        string rootPath,
        CancellationToken キャンセル通知
    )
    {
        キャンセル通知.ThrowIfCancellationRequested();
        if (File.Exists(対象パス))
        {
            if (
                string.Equals(Path.GetExtension(対象パス), ".cs", パス比較規則.文字列比較方式)
                && !パス除外判定器.無視する(対象パス, rootPath, _無視対象パス群)
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
            キャンセル通知.ThrowIfCancellationRequested();
            if (!パス除外判定器.無視する(file, rootPath, _無視対象パス群))
            {
                yield return file;
            }
        }
    }

    private static WorkspaceEventRegistration 作業領域の失敗処理を登録する(
        MSBuildWorkspace workspace,
        List<string> 作業領域失敗群
    ) =>
        workspace.RegisterWorkspaceFailedHandler(args =>
        {
            if (args.Diagnostic.Kind == WorkspaceDiagnosticKind.Failure)
            {
                作業領域失敗群.Add(args.Diagnostic.Message);
            }
        });

    private static string Slnxプロジェクトパスを解決する(string rootPath, string projectPath)
    {
        var normalizedPath = projectPath
            .Replace('\\', Path.DirectorySeparatorChar)
            .Replace('/', Path.DirectorySeparatorChar);
        return Path.GetFullPath(Path.Combine(rootPath, normalizedPath));
    }

    private static void 作業領域の失敗を例外にする(string 対象パス, List<string> 作業領域失敗群)
    {
        if (作業領域失敗群.Count == 0)
        {
            return;
        }

        throw new InvalidOperationException(
            $"Target {対象パス} could not be analyzed reliably because project loading contains errors:\n"
                + string.Join("\n", 作業領域失敗群.Select(message => $"MSBuild: {message}"))
        );
    }

    private static void コンパイル結果を検証する(
        CSharpCompilation compilation,
        List<string> 作業領域失敗群,
        CancellationToken キャンセル通知
    )
    {
        var errors = compilation
            .GetDiagnostics(キャンセル通知)
            .Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error)
            .Take(20)
            .ToArray();

        if (errors.Length == 0 && 作業領域失敗群.Count == 0)
        {
            return;
        }

        var details = errors.Select(error => error.ToString()).ToList();
        details.AddRange(作業領域失敗群.Select(message => $"MSBuild: {message}"));

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
