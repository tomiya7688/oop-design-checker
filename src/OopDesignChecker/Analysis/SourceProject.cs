using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace OopDesignChecker.Analysis;

internal sealed class SourceProject
{
    private readonly IReadOnlyDictionary<SyntaxTree, SemanticModel> _semanticModels;

    public SourceProject(
        string rootPath,
        bool isApplication,
        CSharpCompilation compilation,
        IReadOnlyDictionary<SyntaxTree, SemanticModel> semanticModels
    )
    {
        RootPath = rootPath;
        IsApplication = isApplication;
        Compilation = compilation;
        _semanticModels = semanticModels;
    }

    public string RootPath { get; }

    public bool IsApplication { get; }

    public CSharpCompilation Compilation { get; }

    public IEnumerable<SyntaxTree> SyntaxTrees => Compilation.SyntaxTrees;

    public SemanticModel GetSemanticModel(SyntaxTree syntaxTree) => _semanticModels[syntaxTree];
}
