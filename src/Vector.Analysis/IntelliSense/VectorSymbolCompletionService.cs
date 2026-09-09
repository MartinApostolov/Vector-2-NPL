namespace Vector.Analysis.IntelliSense;

using Vector.Analysis.Documents;
using Vector.Analysis.Symbols;

public sealed class VectorSymbolCompletionService
{
    public IReadOnlyList<VectorCatalogItem> GetCompletions(
        VectorAnalysisResult analysis,
        int utf16Offset,
        string prefix)
    {
        ArgumentNullException.ThrowIfNull(analysis);
        return analysis.SemanticModel.GetVisibleSymbols(utf16Offset)
            .Where(symbol => symbol.Name.StartsWith(prefix, StringComparison.Ordinal))
            .Select(ToCatalogItem)
            .ToArray();
    }

    private static VectorCatalogItem ToCatalogItem(VectorSymbol symbol) => symbol.Kind switch
    {
        VectorSymbolKind.Function => new VectorCatalogItem(
            symbol.Name,
            VectorCatalogItemKind.Function,
            $"{symbol.Name}({string.Join(", ", symbol.Parameters)})",
            "Function declared in the current Vector document."),
        VectorSymbolKind.Parameter => new VectorCatalogItem(
            symbol.Name,
            VectorCatalogItemKind.Parameter,
            "Vector parameter",
            "Function parameter in the current lexical scope."),
        VectorSymbolKind.LoopVariable => new VectorCatalogItem(
            symbol.Name,
            VectorCatalogItemKind.Variable,
            "Vector loop variable",
            "Iteration variable in the current for-loop scope."),
        _ => new VectorCatalogItem(
            symbol.Name,
            VectorCatalogItemKind.Variable,
            "Vector variable",
            "Variable declared in the current lexical scope."),
    };
}
