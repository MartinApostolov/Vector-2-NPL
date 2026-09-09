namespace Vector.Analysis.IntelliSense;

using Vector.Analysis.Documents;
using Vector.Core.Syntax.Statements;

public sealed class VectorCompletionService
{
    private readonly VectorSymbolCompletionService symbols = new();

    public IReadOnlyList<VectorCatalogItem> GetCompletions(VectorAnalysisResult analysis, int utf16Offset)
    {
        ArgumentNullException.ThrowIfNull(analysis);
        if (utf16Offset < 0 || utf16Offset > analysis.Document.Text.Length)
        {
            throw new ArgumentOutOfRangeException(nameof(utf16Offset));
        }

        string prefix = GetQualifiedPrefix(analysis.Document.Text, utf16Offset);
        int lastDot = prefix.LastIndexOf('.');
        if (lastDot >= 0)
        {
            string qualifier = prefix[..lastDot];
            string memberPrefix = prefix[(lastDot + 1)..];
            VectorModuleDescriptor? module = VectorLanguageCatalog.StandardModules
                .SingleOrDefault(candidate => candidate.QualifiedName == qualifier);
            if (module is null)
            {
                return VectorLanguageCatalog.StandardModules
                    .Where(candidate => candidate.QualifiedName.StartsWith(prefix, StringComparison.Ordinal))
                    .Select(ToCatalogItem)
                    .ToArray();
            }

            bool imported = analysis.Syntax.Statements.OfType<ImportStatement>()
                .Any(import => import.QualifiedPath == module.QualifiedName);
            if (!imported)
            {
                return [];
            }

            return module.Functions.Select(ToCatalogItem)
                .Concat(module.Constants)
                .Where(item => item.Label.StartsWith(memberPrefix, StringComparison.Ordinal))
                .OrderBy(item => item.Label, StringComparer.Ordinal)
                .ToArray();
        }

        IEnumerable<VectorCatalogItem> items = VectorLanguageCatalog.Keywords
            .Concat(VectorLanguageCatalog.Builtins.Select(ToCatalogItem))
            .Concat(VectorLanguageCatalog.StandardModules.Select(ToCatalogItem))
            .Concat(this.symbols.GetCompletions(analysis, utf16Offset, prefix));
        return items.Where(item => item.Label.StartsWith(prefix, StringComparison.Ordinal))
            .OrderBy(item => item.Label, StringComparer.Ordinal)
            .ToArray();
    }

    private static VectorCatalogItem ToCatalogItem(VectorCallableDescriptor function) =>
        new(function.Name, VectorCatalogItemKind.Function, function.Signature, function.Documentation);

    private static VectorCatalogItem ToCatalogItem(VectorModuleDescriptor module) =>
        new(module.QualifiedName, VectorCatalogItemKind.Module, "Vector standard module", module.Documentation);

    internal static string GetQualifiedPrefix(string text, int offset)
    {
        int start = offset;
        while (start > 0 && IsIdentifierOrDot(text[start - 1]))
        {
            start--;
        }

        return text[start..offset];
    }

    internal static bool IsIdentifierOrDot(char value) => value == '.' || value == '_' || char.IsLetterOrDigit(value);
}
