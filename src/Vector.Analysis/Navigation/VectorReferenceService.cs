namespace Vector.Analysis.Navigation;

using Vector.Analysis.Documents;
using Vector.Analysis.Modules;
using Vector.Analysis.Symbols;
using Vector.Analysis.Text;
using Vector.Core.Source;

public sealed record VectorReferenceLocation(Uri Uri, TextRange Range, bool IsDeclaration);

public sealed record VectorResolvedSymbol(VectorAnalysisResult Analysis, VectorSymbol Symbol);

public sealed class VectorReferenceService
{
    private readonly VectorModuleIndex modules;

    public VectorReferenceService(VectorModuleIndex modules)
    {
        this.modules = modules ?? throw new ArgumentNullException(nameof(modules));
    }

    public IReadOnlyList<VectorReferenceLocation> GetReferences(
        VectorAnalysisResult analysis,
        int utf16Offset,
        IEnumerable<VectorAnalysisResult> workspaceDocuments,
        bool includeDeclaration,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(analysis);
        ArgumentNullException.ThrowIfNull(workspaceDocuments);
        cancellationToken.ThrowIfCancellationRequested();

        VectorResolvedSymbol? target = this.ResolveSymbol(analysis, utf16Offset, cancellationToken)
            ?? this.ResolveQualifiedExpressionMember(analysis, utf16Offset, cancellationToken);
        if (target is null || target.Symbol.IsSynthetic || target.Symbol.IsDuplicate)
        {
            return [];
        }

        var locations = new List<VectorReferenceLocation>();
        if (includeDeclaration)
        {
            Add(locations, target.Analysis, target.Symbol.DeclarationSpan, isDeclaration: true);
        }

        foreach (VectorReference reference in target.Analysis.SemanticModel.References)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (ReferenceEquals(reference.Symbol, target.Symbol))
            {
                Add(locations, target.Analysis, reference.Span, isDeclaration: false);
            }
        }

        if (target.Symbol.ContainingSymbol is null)
        {
            foreach (VectorAnalysisResult document in workspaceDocuments)
            {
                this.AddQualifiedMemberReferences(document, target, locations, cancellationToken);
            }
        }

        return locations
            .DistinctBy(location => new LocationKey(
                location.Uri,
                location.Range.Start.Line,
                location.Range.Start.Character,
                location.Range.End.Line,
                location.Range.End.Character))
            .OrderBy(location => location.Uri.AbsoluteUri, StringComparer.Ordinal)
            .ThenBy(location => location.Range.Start.Line)
            .ThenBy(location => location.Range.Start.Character)
            .ToArray();
    }

    public VectorResolvedSymbol? ResolveSymbol(
        VectorAnalysisResult analysis,
        int utf16Offset,
        CancellationToken cancellationToken = default)
    {
        VectorSymbol? declaration = analysis.SemanticModel.Symbols.FirstOrDefault(symbol =>
            Contains(symbol.DeclarationSpan, utf16Offset));
        if (declaration is not null)
        {
            return new VectorResolvedSymbol(analysis, declaration);
        }

        VectorReference? reference = analysis.SemanticModel.References.FirstOrDefault(candidate =>
            Contains(candidate.Span, utf16Offset));
        if (reference?.Symbol is not null)
        {
            return new VectorResolvedSymbol(analysis, reference.Symbol);
        }

        VectorQualifiedReference? qualified = analysis.SemanticModel.QualifiedReferences.FirstOrDefault(candidate =>
            candidate.SegmentSpans.Count == candidate.PathSegments.Count
            && candidate.SegmentSpans.Count >= 2
            && Contains(candidate.SegmentSpans[^1], utf16Offset));
        return qualified is null ? null : this.ResolveQualifiedMember(analysis, qualified, cancellationToken);
    }

    private VectorResolvedSymbol? ResolveQualifiedMember(
        VectorAnalysisResult importingAnalysis,
        VectorQualifiedReference reference,
        CancellationToken cancellationToken)
    {
        string moduleName = string.Join('.', reference.PathSegments.Take(reference.PathSegments.Count - 1));
        string memberName = reference.PathSegments[^1];
        if (!this.modules.TryGetImportedModule(
            importingAnalysis,
            moduleName,
            out VectorModuleAnalysis? module,
            cancellationToken))
        {
            return null;
        }

        VectorSymbol? member = module!.Members.FirstOrDefault(symbol =>
            !symbol.IsDuplicate && symbol.Name == memberName);
        return member is null ? null : new VectorResolvedSymbol(module.Analysis, member);
    }

    private VectorResolvedSymbol? ResolveQualifiedExpressionMember(
        VectorAnalysisResult analysis,
        int utf16Offset,
        CancellationToken cancellationToken)
    {
        VectorQualifiedReference? qualified = analysis.SemanticModel.QualifiedReferences.FirstOrDefault(candidate =>
            candidate.PathSegments.Count >= 2 && Contains(candidate.Span, utf16Offset));
        return qualified is null ? null : this.ResolveQualifiedMember(analysis, qualified, cancellationToken);
    }

    private void AddQualifiedMemberReferences(
        VectorAnalysisResult document,
        VectorResolvedSymbol target,
        List<VectorReferenceLocation> locations,
        CancellationToken cancellationToken)
    {
        foreach (VectorQualifiedReference reference in document.SemanticModel.QualifiedReferences)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (reference.PathSegments.Count < 2
                || reference.SegmentSpans.Count != reference.PathSegments.Count
                || reference.PathSegments[^1] != target.Symbol.Name)
            {
                continue;
            }

            VectorResolvedSymbol? candidate = this.ResolveQualifiedMember(document, reference, cancellationToken);
            if (candidate is not null && SameSymbol(candidate, target))
            {
                Add(locations, document, reference.SegmentSpans[^1], isDeclaration: false);
            }
        }
    }

    private static bool SameSymbol(VectorResolvedSymbol left, VectorResolvedSymbol right) =>
        left.Analysis.Document.Uri == right.Analysis.Document.Uri
        && left.Symbol.DeclarationSpan.Start.Offset == right.Symbol.DeclarationSpan.Start.Offset
        && left.Symbol.DeclarationSpan.End.Offset == right.Symbol.DeclarationSpan.End.Offset;

    private static void Add(
        List<VectorReferenceLocation> locations,
        VectorAnalysisResult analysis,
        SourceSpan span,
        bool isDeclaration) => locations.Add(new VectorReferenceLocation(
            analysis.Document.Uri,
            analysis.Document.LineMap.GetRange(span),
            isDeclaration));

    private static bool Contains(SourceSpan span, int offset) =>
        offset >= span.Start.Offset && offset < span.End.Offset;

    private sealed record LocationKey(
        Uri Uri,
        int StartLine,
        int StartCharacter,
        int EndLine,
        int EndCharacter);
}
