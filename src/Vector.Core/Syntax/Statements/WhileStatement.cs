using Vector.Core.Source;

namespace Vector.Core.Syntax.Statements;

/// <summary>
/// Represents a while loop.
/// </summary>
public sealed class WhileStatement : StatementSyntax
{
    public WhileStatement(ExpressionSyntax condition, BlockStatement body, SourceSpan span)
        : base(span)
    {
        Condition = condition ?? throw new ArgumentNullException(nameof(condition));
        Body = body ?? throw new ArgumentNullException(nameof(body));
    }

    public ExpressionSyntax Condition { get; }

    public BlockStatement Body { get; }
}
