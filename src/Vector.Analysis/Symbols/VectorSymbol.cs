namespace Vector.Analysis.Symbols;

using Vector.Core.Source;

public enum VectorSymbolKind
{
    Variable,
    Function,
    Parameter,
    LoopVariable,
}

public sealed class VectorSymbol
{
    private readonly List<VectorSymbol> children = [];

    internal VectorSymbol(
        string name,
        VectorSymbolKind kind,
        SourceSpan declarationSpan,
        SourceSpan fullSpan,
        int availableFrom,
        IReadOnlyList<string>? parameters,
        VectorSymbol? containingSymbol)
    {
        this.Name = name;
        this.Kind = kind;
        this.DeclarationSpan = declarationSpan;
        this.FullSpan = fullSpan;
        this.AvailableFrom = availableFrom;
        this.Parameters = parameters ?? [];
        this.ContainingSymbol = containingSymbol;
    }

    public string Name { get; }

    public VectorSymbolKind Kind { get; }

    public SourceSpan DeclarationSpan { get; }

    public SourceSpan FullSpan { get; }

    public IReadOnlyList<string> Parameters { get; }

    public VectorSymbol? ContainingSymbol { get; }

    public IReadOnlyList<VectorSymbol> Children => this.children;

    public bool IsDuplicate { get; internal set; }

    public bool IsSynthetic => this.Name == "<missing>";

    internal int AvailableFrom { get; }

    internal void AddChild(VectorSymbol child) => this.children.Add(child);
}
