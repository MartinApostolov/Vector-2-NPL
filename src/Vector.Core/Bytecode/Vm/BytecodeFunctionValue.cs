using Vector.Core.Runtime.Values;
using RuntimeEnvironment = Vector.Core.Runtime.Environment;

namespace Vector.Core.Bytecode.Vm;

/// <summary>
/// Runtime closure created from a compiled Vector function prototype.
/// </summary>
internal sealed class BytecodeFunctionValue : FunctionValue
{
    public BytecodeFunctionValue(
        BytecodeFunctionPrototype prototype,
        RuntimeEnvironment closure)
    {
        Prototype = prototype ?? throw new ArgumentNullException(nameof(prototype));
        Closure = closure ?? throw new ArgumentNullException(nameof(closure));
    }

    public BytecodeFunctionPrototype Prototype { get; }

    public string Name => Prototype.Name;

    public int Arity => Prototype.Arity;

    public RuntimeEnvironment Closure { get; }
}
