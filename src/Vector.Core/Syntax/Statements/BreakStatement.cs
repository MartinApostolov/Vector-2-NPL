using Vector.Core.Source;

namespace Vector.Core.Syntax.Statements;

/// <summary>
/// Represents a break statement.
/// </summary>
public sealed class BreakStatement : StatementSyntax
{
    public BreakStatement(SourceSpan span)
        : base(span)
    {
    }
}
