using Vector.Core.Lexing;
using Vector.Core.Source;

namespace Vector.Core.Syntax.Expressions;

/// <summary>
/// Represents a binary operator with left and right operands.
/// </summary>
public sealed class BinaryExpression : ExpressionSyntax
{
    public BinaryExpression(
        ExpressionSyntax left,
        Token operatorToken,
        ExpressionSyntax right,
        SourceSpan span)
        : base(span)
    {
        Left = left ?? throw new ArgumentNullException(nameof(left));
        OperatorToken = operatorToken ?? throw new ArgumentNullException(nameof(operatorToken));
        Right = right ?? throw new ArgumentNullException(nameof(right));
    }

    public ExpressionSyntax Left { get; }

    public Token OperatorToken { get; }

    public ExpressionSyntax Right { get; }
}
