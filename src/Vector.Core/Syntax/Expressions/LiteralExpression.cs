using Vector.Core.Source;

namespace Vector.Core.Syntax.Expressions;

/// <summary>
/// Represents a literal value such as a number, text, boolean, or nothing.
/// </summary>
public sealed class LiteralExpression : ExpressionSyntax
{
    public LiteralExpression(object? value, SourceSpan span)
        : base(span)
    {
        Value = value;
    }

    public object? Value { get; }
}
