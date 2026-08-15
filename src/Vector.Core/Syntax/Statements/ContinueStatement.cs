using Vector.Core.Source;

namespace Vector.Core.Syntax.Statements;

/// <summary>
/// Represents a continue statement.
/// </summary>
public sealed class ContinueStatement : StatementSyntax
{
    public ContinueStatement(SourceSpan span)
        : base(span)
    {
    }
}
