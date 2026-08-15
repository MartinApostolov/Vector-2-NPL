using Vector.Core.Source;

namespace Vector.Core.Syntax.Expressions;

/// <summary>
/// Represents a dotted qualified identifier path such as <c>lib.geometry.distance</c>.
/// </summary>
public sealed class QualifiedNameExpression : ExpressionSyntax
{
    private readonly string[] _pathSegments;

    public QualifiedNameExpression(IEnumerable<string> pathSegments, SourceSpan span)
        : base(span)
    {
        ArgumentNullException.ThrowIfNull(pathSegments);
        _pathSegments = pathSegments.ToArray();

        if (_pathSegments.Length < 2 || _pathSegments.Any(string.IsNullOrEmpty))
        {
            throw new ArgumentException(
                "A qualified name must contain at least two non-empty identifiers.",
                nameof(pathSegments));
        }
    }

    public IReadOnlyList<string> PathSegments => _pathSegments;

    public string QualifiedName => string.Join('.', _pathSegments);
}
