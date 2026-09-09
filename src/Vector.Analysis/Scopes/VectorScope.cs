namespace Vector.Analysis.Scopes;

using Vector.Analysis.Symbols;
using Vector.Core.Source;

public enum VectorScopeKind
{
    Module,
    Function,
    Block,
    ForLoop,
}

public sealed class VectorScope
{
    private readonly List<VectorScope> children = [];
    private readonly List<VectorSymbol> declarations = [];
    private readonly Dictionary<string, VectorSymbol> firstDeclarations = new(StringComparer.Ordinal);

    internal VectorScope(VectorScopeKind kind, SourceSpan span, VectorScope? parent, bool includesEnd)
    {
        this.Kind = kind;
        this.Span = span;
        this.Parent = parent;
        this.IncludesEnd = includesEnd;
        parent?.children.Add(this);
    }

    public VectorScopeKind Kind { get; }

    public SourceSpan Span { get; }

    public VectorScope? Parent { get; }

    public IReadOnlyList<VectorScope> Children => this.children;

    public IReadOnlyList<VectorSymbol> Declarations => this.declarations;

    internal bool IncludesEnd { get; }

    internal void Declare(VectorSymbol symbol)
    {
        this.declarations.Add(symbol);
        if (!this.firstDeclarations.TryAdd(symbol.Name, symbol))
        {
            symbol.IsDuplicate = true;
        }
    }

    internal bool Contains(int offset) =>
        offset >= this.Span.Start.Offset
        && (offset < this.Span.End.Offset || (this.IncludesEnd && offset == this.Span.End.Offset));
}
