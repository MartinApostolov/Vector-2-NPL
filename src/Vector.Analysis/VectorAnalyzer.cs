namespace Vector.Analysis;

using Vector.Analysis.Documents;
using Vector.Core.Parsing;

public sealed class VectorAnalyzer
{
    public VectorAnalysisResult Analyze(VectorDocumentSnapshot document, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(document);
        cancellationToken.ThrowIfCancellationRequested();

        var parser = new Parser(document.SourceText);
        var parseResult = parser.ParseCompilationUnit();
        cancellationToken.ThrowIfCancellationRequested();

        return new VectorAnalysisResult(
            document,
            parseResult.Root,
            parseResult.Diagnostics.Select(diagnostic => diagnostic.WithSource(document.DisplayName, document.Text)));
    }
}
