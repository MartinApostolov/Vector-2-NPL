using Vector.Core.Source;

namespace Vector.Core.Syntax;

/// <summary>
/// Base type for every node in a Vector syntax tree.
/// </summary>
public abstract class SyntaxNode
{
    protected SyntaxNode(SourceSpan span)
    {
        Span = span;
    }

    public SourceSpan Span { get; }
}

/// <summary>
/// Base type for syntax nodes that produce a value when evaluated.
/// </summary>
public abstract class ExpressionSyntax : SyntaxNode
{
    protected ExpressionSyntax(SourceSpan span)
        : base(span)
    {
    }
}
