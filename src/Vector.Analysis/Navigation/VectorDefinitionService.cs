namespace Vector.Analysis.Navigation;

using Vector.Analysis.Documents;
using Vector.Analysis.IntelliSense;
using Vector.Analysis.Modules;
using Vector.Analysis.Symbols;
using Vector.Analysis.Text;
using Vector.Core.Source;
using Vector.Core.Syntax.Statements;

public sealed record VectorDefinitionLocation(Uri Uri, TextRange Range);

public sealed class VectorDefinitionService
{
    private readonly VectorModuleIndex modules;

    public VectorDefinitionService(VectorModuleIndex modules)
    {
        this.modules = modules ?? throw new ArgumentNullException(nameof(modules));
    }

    public VectorDefinitionLocation? GetDefinition(
        VectorAnalysisResult analysis,
        int utf16Offset,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(analysis);
        cancellationToken.ThrowIfCancellationRequested();

        VectorSymbol? declaration = analysis.SemanticModel.Symbols.FirstOrDefault(symbol =>
            !symbol.IsSynthetic && Contains(symbol.DeclarationSpan, utf16Offset));
        if (declaration is not null)
        {
            return FromSymbol(analysis, declaration);
        }

        VectorReference? reference = analysis.SemanticModel.References.FirstOrDefault(candidate =>
            Contains(candidate.Span, utf16Offset));
        if (reference?.Symbol is not null)
        {
            return FromSymbol(analysis, reference.Symbol);
        }

        ImportStatement? import = analysis.Syntax.Statements.OfType<ImportStatement>()
            .FirstOrDefault(statement => Contains(statement.Span, utf16Offset));
        if (import is not null)
        {
            return this.FromModule(analysis, import.QualifiedPath, memberName: null, cancellationToken);
        }

        VectorQualifiedReference? qualified = analysis.SemanticModel.QualifiedReferences.FirstOrDefault(candidate =>
            Contains(candidate.Span, utf16Offset));
        if (qualified is null || qualified.PathSegments.Count < 2)
        {
            return null;
        }

        string moduleName = string.Join('.', qualified.PathSegments.Take(qualified.PathSegments.Count - 1));
        string memberName = qualified.PathSegments[^1];
        bool onMember = qualified.SegmentSpans.Count == qualified.PathSegments.Count
            && Contains(qualified.SegmentSpans[^1], utf16Offset);
        return this.FromModule(analysis, moduleName, onMember ? memberName : null, cancellationToken);
    }

    private VectorDefinitionLocation? FromModule(
        VectorAnalysisResult importingAnalysis,
        string moduleName,
        string? memberName,
        CancellationToken cancellationToken)
    {
        if (VectorLanguageCatalog.StandardModules.Any(module => module.QualifiedName == moduleName)
            || !this.modules.TryGetImportedModule(
                importingAnalysis,
                moduleName,
                out VectorModuleAnalysis? module,
                cancellationToken))
        {
            return null;
        }

        if (memberName is not null)
        {
            VectorSymbol? member = module!.Members.FirstOrDefault(symbol => symbol.Name == memberName);
            return member is null ? null : FromSymbol(module.Analysis, member);
        }

        return new VectorDefinitionLocation(
            module!.Analysis.Document.Uri,
            new TextRange(new TextPosition(0, 0), new TextPosition(0, 0)));
    }

    private static VectorDefinitionLocation FromSymbol(VectorAnalysisResult analysis, VectorSymbol symbol) =>
        new(analysis.Document.Uri, analysis.Document.LineMap.GetRange(symbol.DeclarationSpan));

    private static bool Contains(SourceSpan span, int offset) =>
        offset >= span.Start.Offset && offset < span.End.Offset;
}
