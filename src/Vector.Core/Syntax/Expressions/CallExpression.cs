using Vector.Core.Source;

namespace Vector.Core.Syntax.Expressions;

/// <summary>
/// Represents calling an expression with zero or more arguments.
/// </summary>
public sealed class CallExpression : ExpressionSyntax
{
    private readonly ExpressionSyntax[] _arguments;

    public CallExpression(
        ExpressionSyntax callee,
        IEnumerable<ExpressionSyntax> arguments,
        SourceSpan span)
        : base(span)
    {
        Callee = callee ?? throw new ArgumentNullException(nameof(callee));
        ArgumentNullException.ThrowIfNull(arguments);
        _arguments = arguments.ToArray();

        if (_arguments.Any(argument => argument is null))
        {
            throw new ArgumentException("Call arguments cannot contain null expressions.", nameof(arguments));
        }
    }

    public ExpressionSyntax Callee { get; }

    public IReadOnlyList<ExpressionSyntax> Arguments => _arguments;
}
