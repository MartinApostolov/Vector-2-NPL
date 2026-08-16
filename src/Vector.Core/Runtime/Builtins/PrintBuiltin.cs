using Vector.Core.Runtime.Host;
using Vector.Core.Runtime.Values;

namespace Vector.Core.Runtime.Builtins;

/// <summary>
/// Implements the required global print(value) function.
/// </summary>
public sealed class PrintBuiltin : BuiltinFunction
{
    private readonly IVectorHost _host;

    public PrintBuiltin(IVectorHost host)
    {
        _host = host ?? throw new ArgumentNullException(nameof(host));
    }

    public override string Name => "print";

    public override int Arity => 1;

    public override VectorValue Call(Interpreter interpreter, IReadOnlyList<VectorValue> arguments)
    {
        ArgumentNullException.ThrowIfNull(interpreter);
        ArgumentNullException.ThrowIfNull(arguments);

        if (arguments.Count != Arity)
        {
            throw new ArgumentException(
                $"Builtin '{Name}' requires {Arity} argument, but received {arguments.Count}.",
                nameof(arguments));
        }

        _host.WriteLine(VectorValueFormatter.Format(arguments[0]));
        return NothingValue.Instance;
    }

}
