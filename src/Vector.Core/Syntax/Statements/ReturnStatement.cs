using Vector.Core.Source;

namespace Vector.Core.Syntax.Statements;

/// <summary>
/// Represents a return statement, optionally with a value expression.
/// </summary>
public sealed class ReturnStatement : StatementSyntax
{
    public ReturnStatement(ExpressionSyntax? expression, SourceSpan span)
        : base(span)
    {
        Expression = expression;
    }

    public ExpressionSyntax? Expression { get; }
}
