using Vector.Core.Source;

namespace Vector.Core.Syntax.Statements;

/// <summary>
/// Represents a for-in loop over an expression.
/// </summary>
public sealed class ForStatement : StatementSyntax
{
    public ForStatement(
        string variableName,
        ExpressionSyntax iterable,
        BlockStatement body,
        SourceSpan span)
        : base(span)
    {
        ArgumentException.ThrowIfNullOrEmpty(variableName);
        VariableName = variableName;
        Iterable = iterable ?? throw new ArgumentNullException(nameof(iterable));
        Body = body ?? throw new ArgumentNullException(nameof(body));
    }

    public string VariableName { get; }

    public ExpressionSyntax Iterable { get; }

    public BlockStatement Body { get; }
}
