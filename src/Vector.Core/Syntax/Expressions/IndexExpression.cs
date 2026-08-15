using Vector.Core.Source;

namespace Vector.Core.Syntax.Expressions;

/// <summary>
/// Represents indexing into another expression.
/// </summary>
public sealed class IndexExpression : ExpressionSyntax
{
    public IndexExpression(ExpressionSyntax target, ExpressionSyntax index, SourceSpan span)
        : base(span)
    {
        Target = target ?? throw new ArgumentNullException(nameof(target));
        Index = index ?? throw new ArgumentNullException(nameof(index));
    }

    public ExpressionSyntax Target { get; }

    public ExpressionSyntax Index { get; }
}
