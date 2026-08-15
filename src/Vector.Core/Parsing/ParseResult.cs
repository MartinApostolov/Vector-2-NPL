using Vector.Core.Diagnostics;
using Vector.Core.Syntax;

namespace Vector.Core.Parsing;

/// <summary>
/// Contains a parser result together with all lexer/parser diagnostics produced for it.
/// </summary>
public sealed class ParseResult<TNode>
    where TNode : SyntaxNode
{
    private readonly Diagnostic[] _diagnostics;

    public ParseResult(TNode root, IEnumerable<Diagnostic> diagnostics)
    {
        Root = root ?? throw new ArgumentNullException(nameof(root));
        ArgumentNullException.ThrowIfNull(diagnostics);
        _diagnostics = diagnostics.ToArray();
    }

    public TNode Root { get; }

    public IReadOnlyList<Diagnostic> Diagnostics => _diagnostics;

    public bool HasErrors => _diagnostics.Any(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error);
}
