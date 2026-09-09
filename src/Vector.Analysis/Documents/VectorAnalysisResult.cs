namespace Vector.Analysis.Documents;

using Vector.Core.Diagnostics;
using Vector.Core.Syntax;
using Vector.Analysis.Scopes;

public sealed class VectorAnalysisResult
{
    private readonly Diagnostic[] diagnostics;

    internal VectorAnalysisResult(
        VectorDocumentSnapshot document,
        CompilationUnit syntax,
        IEnumerable<Diagnostic> diagnostics)
    {
        this.Document = document;
        this.Syntax = syntax;
        this.diagnostics = diagnostics.ToArray();
        this.SemanticModel = VectorScopeBuilder.Build(document, syntax);
    }

    public VectorDocumentSnapshot Document { get; }

    public CompilationUnit Syntax { get; }

    public VectorSemanticModel SemanticModel { get; }

    public IReadOnlyList<Diagnostic> Diagnostics => this.diagnostics;

    public bool HasErrors => this.diagnostics.Any(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error);
}
