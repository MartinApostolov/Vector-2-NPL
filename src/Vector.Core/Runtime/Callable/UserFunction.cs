using Vector.Core.Runtime;
using Vector.Core.Runtime.Values;
using Vector.Core.Syntax.Statements;

namespace Vector.Core.Runtime.Callable;

/// <summary>
/// A user-declared Vector function together with the lexical environment and source context it captured.
/// </summary>
public sealed class UserFunction : FunctionValue, IVectorCallable
{
    public UserFunction(
        FunctionDeclaration declaration,
        Environment closure,
        string? sourceName = null,
        string? sourceText = null)
    {
        Declaration = declaration ?? throw new ArgumentNullException(nameof(declaration));
        Closure = closure ?? throw new ArgumentNullException(nameof(closure));
        SourceName = sourceName;
        SourceText = sourceText;
    }

    public FunctionDeclaration Declaration { get; }

    public string Name => Declaration.Name;

    public int Arity => Declaration.Parameters.Count;

    internal Environment Closure { get; }

    internal string? SourceName { get; }

    internal string? SourceText { get; }

    public VectorValue Call(Interpreter interpreter, IReadOnlyList<VectorValue> arguments)
    {
        ArgumentNullException.ThrowIfNull(interpreter);
        ArgumentNullException.ThrowIfNull(arguments);
        return interpreter.InvokeUserFunction(this, arguments);
    }
}
