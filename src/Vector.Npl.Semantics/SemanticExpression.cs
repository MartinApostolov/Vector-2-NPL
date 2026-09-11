namespace Vector.Npl.Semantics;

/// <summary>
/// The language-independent root of the NPL semantic representation.
/// </summary>
public abstract record SemanticExpression;

public enum ComparisonKind
{
    GT,
    GTE,
    LT,
    LTE,
    EQ,
    NEQ,
}

public sealed record Comparison(
    ComparisonKind Kind,
    SemanticExpression? Left,
    SemanticExpression? Right) : SemanticExpression;

public sealed record Identifier(string? Name) : SemanticExpression;

public sealed record NumberLiteral(double Value) : SemanticExpression;

public sealed record TextLiteral(string? Value) : SemanticExpression;

public sealed record BooleanLiteral(bool Value) : SemanticExpression;

public sealed record CurrentItem : SemanticExpression;
