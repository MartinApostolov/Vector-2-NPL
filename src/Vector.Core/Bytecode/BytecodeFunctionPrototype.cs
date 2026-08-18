using Vector.Core.Source;

namespace Vector.Core.Bytecode;

/// <summary>
/// Describes one compiled Vector function before a runtime closure captures an environment.
/// </summary>
internal sealed class BytecodeFunctionPrototype
{
    private readonly IReadOnlyList<string> _parameters;

    public BytecodeFunctionPrototype(
        string name,
        IEnumerable<string> parameters,
        BytecodeChunk chunk,
        SourceSpan declarationSpan)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("A bytecode function must have a non-empty name.", nameof(name));
        }

        ArgumentNullException.ThrowIfNull(parameters);
        ArgumentNullException.ThrowIfNull(chunk);

        var parameterArray = parameters.ToArray();
        if (parameterArray.Any(string.IsNullOrWhiteSpace))
        {
            throw new ArgumentException("Bytecode function parameter names cannot be empty.", nameof(parameters));
        }

        Name = name;
        _parameters = Array.AsReadOnly(parameterArray);
        Chunk = chunk;
        DeclarationSpan = declarationSpan;
    }

    public string Name { get; }

    public IReadOnlyList<string> Parameters => _parameters;

    public int Arity => _parameters.Count;

    public BytecodeChunk Chunk { get; }

    public SourceSpan DeclarationSpan { get; }
}
