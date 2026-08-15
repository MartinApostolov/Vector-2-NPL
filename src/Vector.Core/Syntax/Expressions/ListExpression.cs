using Vector.Core.Source;

namespace Vector.Core.Syntax.Expressions;

/// <summary>
/// Represents a list literal.
/// </summary>
public sealed class ListExpression : ExpressionSyntax
{
    private readonly ExpressionSyntax[] _elements;

    public ListExpression(IEnumerable<ExpressionSyntax> elements, SourceSpan span)
        : base(span)
    {
        ArgumentNullException.ThrowIfNull(elements);
        _elements = elements.ToArray();

        if (_elements.Any(element => element is null))
        {
            throw new ArgumentException("List elements cannot contain null expressions.", nameof(elements));
        }
    }

    public IReadOnlyList<ExpressionSyntax> Elements => _elements;
}
