namespace Vector.Analysis.IntelliSense;

using Vector.Analysis.Documents;
using Vector.Analysis.Modules;
using Vector.Analysis.Symbols;
using Vector.Analysis.Text;
using Vector.Core.Source;

public sealed record VectorHoverInfo(string Markdown, TextRange Range);

public sealed class VectorHoverService
{
    private readonly VectorModuleIndex? modules;

    public VectorHoverService(VectorModuleIndex? modules = null)
    {
        this.modules = modules;
    }

    public VectorHoverInfo? GetHover(VectorAnalysisResult analysis, int utf16Offset)
    {
        ArgumentNullException.ThrowIfNull(analysis);
        string text = analysis.Document.Text;
        if (utf16Offset < 0 || utf16Offset > text.Length)
        {
            throw new ArgumentOutOfRangeException(nameof(utf16Offset));
        }

        int start = utf16Offset;
        int end = utf16Offset;
        while (start > 0 && VectorCompletionService.IsIdentifierOrDot(text[start - 1]))
        {
            start--;
        }

        while (end < text.Length && VectorCompletionService.IsIdentifierOrDot(text[end]))
        {
            end++;
        }

        if (start == end)
        {
            return null;
        }

        string token = text[start..end];
        (string signature, string documentation)? description = this.DescribeSourceSymbol(
            analysis,
            utf16Offset,
            out SourceSpan? symbolSpan);
        description ??= Describe(token);
        if (description is null)
        {
            return null;
        }

        return new VectorHoverInfo(
            $"```vector\n{description.Value.signature}\n```\n\n{description.Value.documentation}",
            symbolSpan is SourceSpan span
                ? analysis.Document.LineMap.GetRange(span)
                : new TextRange(analysis.Document.LineMap.GetPosition(start), analysis.Document.LineMap.GetPosition(end)));
    }

    private (string Signature, string Documentation)? DescribeSourceSymbol(
        VectorAnalysisResult analysis,
        int utf16Offset,
        out SourceSpan? hoverSpan)
    {
        VectorSymbol? symbol = analysis.SemanticModel.Symbols.FirstOrDefault(candidate =>
            !candidate.IsSynthetic && Contains(candidate.DeclarationSpan, utf16Offset));
        hoverSpan = symbol?.DeclarationSpan;

        if (symbol is null)
        {
            VectorReference? reference = analysis.SemanticModel.References.FirstOrDefault(candidate =>
                Contains(candidate.Span, utf16Offset));
            symbol = reference?.Symbol;
            hoverSpan = reference?.Span;
        }

        if (symbol is not null)
        {
            return DescribeSymbol(symbol, qualifier: null);
        }

        VectorQualifiedReference? qualified = analysis.SemanticModel.QualifiedReferences.FirstOrDefault(candidate =>
            Contains(candidate.Span, utf16Offset));
        if (qualified is null || qualified.PathSegments.Count < 2 || this.modules is null)
        {
            return null;
        }

        int memberIndex = qualified.PathSegments.Count - 1;
        if (qualified.SegmentSpans.Count != qualified.PathSegments.Count
            || !Contains(qualified.SegmentSpans[memberIndex], utf16Offset))
        {
            return null;
        }

        string moduleName = string.Join('.', qualified.PathSegments.Take(memberIndex));
        if (!this.modules.TryGetImportedModule(analysis, moduleName, out VectorModuleAnalysis? module))
        {
            return null;
        }

        VectorSymbol? member = module!.Members.FirstOrDefault(candidate => candidate.Name == qualified.PathSegments[^1]);
        if (member is null)
        {
            return null;
        }

        hoverSpan = qualified.SegmentSpans[memberIndex];
        return DescribeSymbol(member, moduleName);
    }

    private static (string Signature, string Documentation) DescribeSymbol(VectorSymbol symbol, string? qualifier)
    {
        string name = qualifier is null ? symbol.Name : $"{qualifier}.{symbol.Name}";
        return symbol.Kind switch
        {
            VectorSymbolKind.Function => (
                $"{name}({string.Join(", ", symbol.Parameters)})",
                "Vector function declared in source."),
            VectorSymbolKind.Parameter => (name, "Vector function parameter."),
            VectorSymbolKind.LoopVariable => (name, "Vector loop variable."),
            _ => (name, "Vector variable declared in source."),
        };
    }

    private static (string Signature, string Documentation)? Describe(string token)
    {
        VectorCatalogItem? keyword = VectorLanguageCatalog.Keywords.SingleOrDefault(item => item.Label == token);
        if (keyword is not null)
        {
            return (keyword.Label, keyword.Documentation);
        }

        VectorCallableDescriptor? builtin = VectorLanguageCatalog.Builtins.SingleOrDefault(item => item.Name == token);
        if (builtin is not null)
        {
            return (builtin.Signature, builtin.Documentation);
        }

        VectorModuleDescriptor? module = VectorLanguageCatalog.StandardModules
            .SingleOrDefault(item => item.QualifiedName == token);
        if (module is not null)
        {
            return (module.QualifiedName, module.Documentation);
        }

        int dot = token.LastIndexOf('.');
        if (dot < 0)
        {
            return null;
        }

        module = VectorLanguageCatalog.StandardModules.SingleOrDefault(item => item.QualifiedName == token[..dot]);
        if (module is null)
        {
            return null;
        }

        string memberName = token[(dot + 1)..];
        VectorCallableDescriptor? function = module.Functions.SingleOrDefault(item => item.Name == memberName);
        if (function is not null)
        {
            return ($"{module.QualifiedName}.{function.Signature}", function.Documentation);
        }

        VectorCatalogItem? constant = module.Constants.SingleOrDefault(item => item.Label == memberName);
        return constant is null ? null : ($"{module.QualifiedName}.{constant.Label}", constant.Documentation);
    }

    private static bool Contains(SourceSpan span, int offset) =>
        offset >= span.Start.Offset && offset < span.End.Offset;
}
