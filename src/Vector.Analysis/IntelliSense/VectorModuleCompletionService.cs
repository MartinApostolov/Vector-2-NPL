namespace Vector.Analysis.IntelliSense;

using Vector.Analysis.Documents;
using Vector.Analysis.Modules;
using Vector.Analysis.Symbols;

public sealed class VectorModuleCompletionService
{
    private readonly VectorModuleIndex modules;

    public VectorModuleCompletionService(VectorModuleIndex modules)
    {
        this.modules = modules ?? throw new ArgumentNullException(nameof(modules));
    }

    public IReadOnlyList<VectorCatalogItem> GetCompletions(
        VectorAnalysisResult analysis,
        string qualifiedModuleName,
        string memberPrefix,
        CancellationToken cancellationToken = default)
    {
        if (!this.modules.TryGetImportedModule(
            analysis,
            qualifiedModuleName,
            out VectorModuleAnalysis? module,
            cancellationToken))
        {
            return [];
        }

        return module!.Members
            .Where(symbol => symbol.Name.StartsWith(memberPrefix, StringComparison.Ordinal))
            .Select(ToCatalogItem)
            .OrderBy(item => item.Label, StringComparer.Ordinal)
            .ToArray();
    }

    private static VectorCatalogItem ToCatalogItem(VectorSymbol symbol) => symbol.Kind switch
    {
        VectorSymbolKind.Function => new VectorCatalogItem(
            symbol.Name,
            VectorCatalogItemKind.Function,
            $"{symbol.Name}({string.Join(", ", symbol.Parameters)})",
            "Function exported by a local Vector module."),
        _ => new VectorCatalogItem(
            symbol.Name,
            VectorCatalogItemKind.Variable,
            "Vector module variable",
            "Variable exported by a local Vector module."),
    };
}
