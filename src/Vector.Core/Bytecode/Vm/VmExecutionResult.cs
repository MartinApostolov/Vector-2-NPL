using Vector.Core.Runtime.Values;

namespace Vector.Core.Bytecode.Vm;

/// <summary>
/// Result of executing one compiled Vector bytecode program.
/// </summary>
internal sealed class VmExecutionResult
{
    public VmExecutionResult(VectorValue result)
    {
        Result = result ?? throw new ArgumentNullException(nameof(result));
    }

    public VectorValue Result { get; }
}
