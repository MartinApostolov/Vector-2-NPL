using Vector.Core.Diagnostics;
using Vector.Core.Runtime.Callable;
using Vector.Core.Runtime.Values;

namespace Vector.Core.Runtime.Native;

/// <summary>
/// Wraps explicitly registered C#/.NET code as a normal callable Vector value.
/// </summary>
public sealed class NativeFunction : FunctionValue, IVectorCallable
{
    private readonly Func<Interpreter, IReadOnlyList<VectorValue>, VectorValue> _implementation;

    public NativeFunction(
        string name,
        int arity,
        Func<Interpreter, IReadOnlyList<VectorValue>, VectorValue> implementation)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("A native function name cannot be empty.", nameof(name));
        }

        if (arity < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(arity), "Native function arity cannot be negative.");
        }

        Name = name;
        Arity = arity;
        _implementation = implementation ?? throw new ArgumentNullException(nameof(implementation));
    }

    public string Name { get; }

    public int Arity { get; }

    public VectorValue Call(Interpreter interpreter, IReadOnlyList<VectorValue> arguments)
    {
        ArgumentNullException.ThrowIfNull(interpreter);
        ArgumentNullException.ThrowIfNull(arguments);

        if (arguments.Count != Arity)
        {
            throw new ArgumentException(
                $"Native function '{Name}' requires {Arity} arguments, but received {arguments.Count}.",
                nameof(arguments));
        }

        try
        {
            var result = _implementation(interpreter, arguments);
            if (result is null)
            {
                throw new NativeRuntimeException(
                    DiagnosticCode.NativeRuntimeFailure,
                    $"Native function '{Name}' returned an invalid null result.");
            }

            NativeValueConverter.ValidateOutboundValue(result);
            return result;
        }
        catch (NativeRuntimeException)
        {
            throw;
        }
        catch (Exception exception)
        {
            throw new NativeRuntimeException(
                DiagnosticCode.NativeRuntimeFailure,
                $"Native function '{Name}' failed.",
                exception);
        }
    }
}
