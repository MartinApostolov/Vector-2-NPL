using Vector.Core.Source;

namespace Vector.Core.Syntax.Statements;

/// <summary>
/// Represents a named function declaration.
/// </summary>
public sealed class FunctionDeclaration : StatementSyntax
{
    private readonly string[] _parameters;

    public FunctionDeclaration(
        string name,
        IEnumerable<string> parameters,
        BlockStatement body,
        SourceSpan span)
        : base(span)
    {
        ArgumentException.ThrowIfNullOrEmpty(name);
        ArgumentNullException.ThrowIfNull(parameters);

        Name = name;
        _parameters = parameters.ToArray();
        Body = body ?? throw new ArgumentNullException(nameof(body));

        if (_parameters.Any(string.IsNullOrEmpty))
        {
            throw new ArgumentException("Function parameter names cannot be null or empty.", nameof(parameters));
        }
    }

    public string Name { get; }

    public IReadOnlyList<string> Parameters => _parameters;

    public BlockStatement Body { get; }
}
