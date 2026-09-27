using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace OopDesignChecker.Analysis;

internal sealed class SourceProject
{
    private readonly IReadOnlyDictionary<SyntaxTree, SemanticModel> _semanticModels;
    private readonly IReadOnlyList<SyntaxTree> _syntaxTrees;

    public SourceProject(
        string rootPath,
        bool isApplication,
        CSharpCompilation compilation,
        IReadOnlyDictionary<SyntaxTree, SemanticModel> semanticModels,
        IReadOnlyList<SyntaxTree>? syntaxTrees = null
    )
    {
        RootPath = rootPath;
        IsApplication = isApplication;
        Compilation = compilation;
        _semanticModels = semanticModels;
        _syntaxTrees = syntaxTrees ?? compilation.SyntaxTrees.ToArray();
    }

    public string RootPath { get; }

    public bool IsApplication { get; }

    public CSharpCompilation Compilation { get; }

    public IEnumerable<SyntaxTree> SyntaxTrees => _syntaxTrees;

    public SemanticModel GetSemanticModel(SyntaxTree syntaxTree) => _semanticModels[syntaxTree];
}
