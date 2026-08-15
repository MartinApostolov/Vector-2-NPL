using Vector.Core.Source;

namespace Vector.Core.Syntax;

/// <summary>
/// Root syntax node for one Vector source file.
/// </summary>
public sealed class CompilationUnit : SyntaxNode
{
    private readonly StatementSyntax[] _statements;

    public CompilationUnit(IEnumerable<StatementSyntax> statements, SourceSpan span)
        : base(span)
    {
        ArgumentNullException.ThrowIfNull(statements);
        _statements = statements.ToArray();

        if (_statements.Any(statement => statement is null))
        {
            throw new ArgumentException("A compilation unit cannot contain null statements.", nameof(statements));
        }
    }

    public IReadOnlyList<StatementSyntax> Statements => _statements;
}
