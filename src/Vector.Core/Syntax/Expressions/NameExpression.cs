using Vector.Core.Source;

namespace Vector.Core.Syntax.Expressions;

/// <summary>
/// Represents a reference to a named binding.
/// </summary>
public sealed class NameExpression : ExpressionSyntax
{
    public NameExpression(string name, SourceSpan span)
        : base(span)
    {
        ArgumentException.ThrowIfNullOrEmpty(name);
        Name = name;
    }

    public string Name { get; }
}
