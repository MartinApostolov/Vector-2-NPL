using Vector.Core.Source;

namespace Vector.Core.Syntax.Statements;

/// <summary>
/// Represents an expression used as a statement.
/// </summary>
public sealed class ExpressionStatement : StatementSyntax
{
    public ExpressionStatement(ExpressionSyntax expression, SourceSpan span)
        : base(span)
    {
        Expression = expression ?? throw new ArgumentNullException(nameof(expression));
    }

    public ExpressionSyntax Expression { get; }
}
