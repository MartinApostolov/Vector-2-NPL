namespace Vector.Analysis.Documents;

using Vector.Core.Diagnostics;
using Vector.Core.Syntax;

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
    }

    public VectorDocumentSnapshot Document { get; }

    public CompilationUnit Syntax { get; }

    public IReadOnlyList<Diagnostic> Diagnostics => this.diagnostics;

    public bool HasErrors => this.diagnostics.Any(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error);
}
