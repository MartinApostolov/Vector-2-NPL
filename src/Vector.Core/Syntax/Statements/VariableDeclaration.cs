using Vector.Core.Source;

namespace Vector.Core.Syntax.Statements;

/// <summary>
/// Represents a <c>let</c> declaration with a required initializer.
/// </summary>
public sealed class VariableDeclaration : StatementSyntax
{
    public VariableDeclaration(string name, ExpressionSyntax initializer, SourceSpan span)
        : base(span)
    {
        ArgumentException.ThrowIfNullOrEmpty(name);
        Name = name;
        Initializer = initializer ?? throw new ArgumentNullException(nameof(initializer));
    }

    public string Name { get; }

    public ExpressionSyntax Initializer { get; }
}
