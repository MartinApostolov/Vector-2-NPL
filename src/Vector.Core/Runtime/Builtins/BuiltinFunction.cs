using Vector.Core.Diagnostics;
using Vector.Core.Runtime.Callable;
using Vector.Core.Runtime.Values;

namespace Vector.Core.Runtime.Builtins;

/// <summary>
/// Base type for host-provided Vector functions.
/// </summary>
public abstract class BuiltinFunction : FunctionValue, IVectorCallable
{
    public abstract string Name { get; }

    public abstract int Arity { get; }

    public abstract VectorValue Call(Interpreter interpreter, IReadOnlyList<VectorValue> arguments);

    protected static Exception RuntimeFailure(DiagnosticCode code, string message) =>
        new BuiltinRuntimeException(code, message);
}

internal sealed class BuiltinRuntimeException : Exception
{
    public BuiltinRuntimeException(DiagnosticCode code, string message)
        : base(message)
    {
        Code = code;
    }

    public DiagnosticCode Code { get; }
}
