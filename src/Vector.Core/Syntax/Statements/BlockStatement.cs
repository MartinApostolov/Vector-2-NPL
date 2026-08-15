using Vector.Core.Source;

namespace Vector.Core.Syntax.Statements;

/// <summary>
/// Represents a brace-delimited sequence of statements.
/// </summary>
public sealed class BlockStatement : StatementSyntax
{
    private readonly StatementSyntax[] _statements;

    public BlockStatement(IEnumerable<StatementSyntax> statements, SourceSpan span)
        : base(span)
    {
        ArgumentNullException.ThrowIfNull(statements);
        _statements = statements.ToArray();

        if (_statements.Any(statement => statement is null))
        {
            throw new ArgumentException("Block statements cannot contain null statements.", nameof(statements));
        }
    }

    public IReadOnlyList<StatementSyntax> Statements => _statements;
}
