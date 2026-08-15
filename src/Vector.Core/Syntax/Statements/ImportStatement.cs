using Vector.Core.Source;

namespace Vector.Core.Syntax.Statements;

/// <summary>
/// Represents a qualified module import such as <c>import lib.geometry;</c>.
/// </summary>
public sealed class ImportStatement : StatementSyntax
{
    private readonly string[] _pathSegments;

    public ImportStatement(IEnumerable<string> pathSegments, SourceSpan span)
        : base(span)
    {
        ArgumentNullException.ThrowIfNull(pathSegments);
        _pathSegments = pathSegments.ToArray();

        if (_pathSegments.Length == 0 || _pathSegments.Any(string.IsNullOrEmpty))
        {
            throw new ArgumentException(
                "An import path must contain one or more non-empty identifiers.",
                nameof(pathSegments));
        }
    }

    public IReadOnlyList<string> PathSegments => _pathSegments;

    public string QualifiedPath => string.Join('.', _pathSegments);
}
