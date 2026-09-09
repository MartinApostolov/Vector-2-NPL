namespace Vector.Analysis.Scopes;

using Vector.Analysis.Symbols;

public sealed class VectorSemanticModel
{
    private readonly VectorSymbol[] symbols;
    private VectorReference[] references = [];
    private VectorQualifiedReference[] qualifiedReferences = [];

    internal VectorSemanticModel(VectorScope rootScope, IEnumerable<VectorSymbol> symbols)
    {
        this.RootScope = rootScope;
        this.symbols = symbols.ToArray();
    }

    public VectorScope RootScope { get; }

    public IReadOnlyList<VectorSymbol> Symbols => this.symbols;

    public IReadOnlyList<VectorReference> References => this.references;

    public IReadOnlyList<VectorQualifiedReference> QualifiedReferences => this.qualifiedReferences;

    public VectorScope GetScopeAt(int utf16Offset)
    {
        if (!this.RootScope.Contains(utf16Offset))
        {
            throw new ArgumentOutOfRangeException(nameof(utf16Offset));
        }

        VectorScope current = this.RootScope;
        while (true)
        {
            VectorScope? child = current.Children.LastOrDefault(candidate => candidate.Contains(utf16Offset));
            if (child is null)
            {
                return current;
            }

            current = child;
        }
    }

    public IReadOnlyList<VectorSymbol> GetVisibleSymbols(int utf16Offset)
    {
        VectorScope? scope = this.GetScopeAt(utf16Offset);
        var visible = new List<VectorSymbol>();
        var names = new HashSet<string>(StringComparer.Ordinal);

        while (scope is not null)
        {
            foreach (VectorSymbol symbol in scope.Declarations
                .Where(symbol => !symbol.IsDuplicate && symbol.AvailableFrom <= utf16Offset)
                .Reverse())
            {
                if (names.Add(symbol.Name))
                {
                    visible.Add(symbol);
                }
            }

            scope = scope.Parent;
        }

        return visible;
    }

    public bool TryResolveSymbol(string name, int utf16Offset, out VectorSymbol? symbol)
    {
        ArgumentException.ThrowIfNullOrEmpty(name);
        symbol = this.GetVisibleSymbols(utf16Offset).FirstOrDefault(candidate => candidate.Name == name);
        return symbol is not null;
    }

    internal void SetReferences(
        IEnumerable<VectorReference> references,
        IEnumerable<VectorQualifiedReference> qualifiedReferences)
    {
        this.references = references.ToArray();
        this.qualifiedReferences = qualifiedReferences.ToArray();
    }
}
