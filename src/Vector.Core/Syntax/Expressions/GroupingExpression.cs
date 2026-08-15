using Vector.Core.Source;

namespace Vector.Core.Syntax.Expressions;

/// <summary>
/// Represents an expression explicitly grouped with parentheses.
/// </summary>
public sealed class GroupingExpression : ExpressionSyntax
{
    public GroupingExpression(ExpressionSyntax expression, SourceSpan span)
        : base(span)
    {
        Expression = expression ?? throw new ArgumentNullException(nameof(expression));
    }

    public ExpressionSyntax Expression { get; }
}
