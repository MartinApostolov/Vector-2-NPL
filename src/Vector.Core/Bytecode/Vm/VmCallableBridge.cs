using Vector.Core.Diagnostics;
using Vector.Core.Modules;
using Vector.Core.Runtime;
using Vector.Core.Runtime.Builtins;
using Vector.Core.Runtime.Callable;
using Vector.Core.Runtime.Host;
using Vector.Core.Runtime.Native;
using Vector.Core.Runtime.Values;
using Vector.Core.Source;
using RuntimeEnvironment = Vector.Core.Runtime.Environment;

namespace Vector.Core.Bytecode.Vm;

/// <summary>
/// Adapts existing interpreter-backed callables to VM execution while preserving
/// Vector call validation, host services, environment access, and runtime diagnostics.
/// </summary>
internal sealed class VmCallableBridge
{
    private readonly IVectorHost _host;
    private readonly ModuleLoader? _moduleLoader;

    public VmCallableBridge(IVectorHost host, ModuleLoader? moduleLoader = null)
    {
        _host = host ?? throw new ArgumentNullException(nameof(host));
        _moduleLoader = moduleLoader;
    }

    public void ValidateCall(
        VectorValue callee,
        int argumentCount,
        SourceSpan calleeSpan,
        SourceSpan callSpan)
    {
        ArgumentNullException.ThrowIfNull(callee);

        var arity = callee switch
        {
            BytecodeFunctionValue function => function.Arity,
            IVectorCallable callable => callable.Arity,
            _ => throw RuntimeOperations.CreateTypeError(
                $"Only functions can be called, but received {callee.TypeName}.",
                calleeSpan)
        };

        if (argumentCount != arity)
        {
            throw new RuntimeError(
                DiagnosticCode.ArgumentCountMismatch,
                $"Function expects {arity} arguments, but received {argumentCount}.",
                callSpan);
        }
    }

    public VectorValue Invoke(
        IVectorCallable callable,
        RuntimeEnvironment environment,
        IReadOnlyList<VectorValue> arguments,
        SourceSpan callSpan)
    {
        ArgumentNullException.ThrowIfNull(callable);
        ArgumentNullException.ThrowIfNull(environment);
        ArgumentNullException.ThrowIfNull(arguments);

        var interpreter = new Interpreter(environment, _host, _moduleLoader);

        try
        {
            return callable.Call(interpreter, arguments);
        }
        catch (BuiltinRuntimeException error)
        {
            throw new RuntimeError(error.Code, error.Message, callSpan);
        }
        catch (NativeRuntimeException error)
        {
            throw new RuntimeError(error.Code, error.Message, callSpan);
        }
    }
}
