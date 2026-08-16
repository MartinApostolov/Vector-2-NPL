using Vector.Core.Runtime.Values;

namespace Vector.Core.Runtime.Builtins;

public sealed class TextBuiltin : BuiltinFunction
{
    public override string Name => "text";

    public override int Arity => 1;

    public override VectorValue Call(Interpreter interpreter, IReadOnlyList<VectorValue> arguments)
    {
        ArgumentNullException.ThrowIfNull(interpreter);
        ArgumentNullException.ThrowIfNull(arguments);

        if (arguments.Count != Arity)
        {
            throw new ArgumentException($"Builtin '{Name}' requires {Arity} argument, but received {arguments.Count}.", nameof(arguments));
        }

        return arguments[0] is TextValue text
            ? text
            : new TextValue(PrintBuiltin.FormatValue(arguments[0]));
    }
}
