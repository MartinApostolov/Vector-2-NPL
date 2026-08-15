using Vector.Core.Lexing;
using Vector.Core.Source;

namespace Vector.Core.Syntax.Expressions;

/// <summary>
/// Represents a unary operator applied to one operand.
/// </summary>
public sealed class UnaryExpression : ExpressionSyntax
{
    public UnaryExpression(Token operatorToken, ExpressionSyntax operand, SourceSpan span)
        : base(span)
    {
        OperatorToken = operatorToken ?? throw new ArgumentNullException(nameof(operatorToken));
        Operand = operand ?? throw new ArgumentNullException(nameof(operand));
    }

    public Token OperatorToken { get; }

    public ExpressionSyntax Operand { get; }
}
