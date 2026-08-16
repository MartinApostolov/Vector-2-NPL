using Vector.Core.Runtime;
using Vector.Core.Runtime.Values;

namespace Vector.Core.Runtime.Callable;

/// <summary>
/// Runtime contract for values that can be invoked with Vector call syntax.
/// </summary>
public interface IVectorCallable
{
    int Arity { get; }

    VectorValue Call(Interpreter interpreter, IReadOnlyList<VectorValue> arguments);
}
