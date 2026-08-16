using System.Text;
using Vector.Core.Diagnostics;
using Vector.Core.Runtime.Values;

namespace Vector.Core.Runtime.Builtins;

public sealed class LengthBuiltin : BuiltinFunction
{
    public override string Name => "length";

    public override int Arity => 1;

    public override VectorValue Call(Interpreter interpreter, IReadOnlyList<VectorValue> arguments)
    {
        ArgumentNullException.ThrowIfNull(interpreter);
        ArgumentNullException.ThrowIfNull(arguments);

        if (arguments.Count != Arity)
        {
            throw new ArgumentException($"Builtin '{Name}' requires {Arity} argument, but received {arguments.Count}.", nameof(arguments));
        }

        return arguments[0] switch
        {
            ListValue list => new NumberValue(list.Count),
            TextValue text => new NumberValue(text.Value.EnumerateRunes().Count()),
            var value => throw RuntimeFailure(
                DiagnosticCode.RuntimeTypeError,
                $"length(value) requires text or list, but received {value.TypeName}.")
        };
    }
}
