using Vector.Core.Source;

namespace Vector.Core.Syntax.Statements;

/// <summary>
/// Represents an if statement with an optional else or else-if branch.
/// </summary>
public sealed class IfStatement : StatementSyntax
{
    public IfStatement(
        ExpressionSyntax condition,
        BlockStatement thenBranch,
        StatementSyntax? elseBranch,
        SourceSpan span)
        : base(span)
    {
        Condition = condition ?? throw new ArgumentNullException(nameof(condition));
        ThenBranch = thenBranch ?? throw new ArgumentNullException(nameof(thenBranch));
        ElseBranch = elseBranch;
    }

    public ExpressionSyntax Condition { get; }

    public BlockStatement ThenBranch { get; }

    /// <summary>
    /// A following <see cref="BlockStatement"/> for <c>else</c>, another
    /// <see cref="IfStatement"/> for <c>else if</c>, or <see langword="null"/>.
    /// </summary>
    public StatementSyntax? ElseBranch { get; }
}
