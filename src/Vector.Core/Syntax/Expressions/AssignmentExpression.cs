using Vector.Core.Lexing;
using Vector.Core.Source;

namespace Vector.Core.Syntax.Expressions;

/// <summary>
/// Represents assignment of a value to an assignable expression.
/// </summary>
public sealed class AssignmentExpression : ExpressionSyntax
{
    public AssignmentExpression(
        ExpressionSyntax target,
        Token equalsToken,
        ExpressionSyntax value,
        SourceSpan span)
        : base(span)
    {
        Target = target ?? throw new ArgumentNullException(nameof(target));
        EqualsToken = equalsToken ?? throw new ArgumentNullException(nameof(equalsToken));
        Value = value ?? throw new ArgumentNullException(nameof(value));
    }

    public ExpressionSyntax Target { get; }

    public Token EqualsToken { get; }

    public ExpressionSyntax Value { get; }
}
