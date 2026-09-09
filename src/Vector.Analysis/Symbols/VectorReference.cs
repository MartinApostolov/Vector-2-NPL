namespace Vector.Analysis.Symbols;

using Vector.Core.Source;

public sealed record VectorReference(string Name, SourceSpan Span, VectorSymbol? Symbol);

public sealed record VectorQualifiedReference(
    IReadOnlyList<string> PathSegments,
    IReadOnlyList<SourceSpan> SegmentSpans,
    SourceSpan Span)
{
    public string QualifiedName => string.Join('.', this.PathSegments);
}
