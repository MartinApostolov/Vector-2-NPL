using Vector.Core.Runtime.Values;

namespace Vector.Core.Runtime.Builtins;

public sealed class TypeBuiltin : BuiltinFunction
{
    public override string Name => "type";

    public override int Arity => 1;

    public override VectorValue Call(Interpreter interpreter, IReadOnlyList<VectorValue> arguments)
    {
        ArgumentNullException.ThrowIfNull(interpreter);
        ArgumentNullException.ThrowIfNull(arguments);

        if (arguments.Count != Arity)
        {
            throw new ArgumentException($"Builtin '{Name}' requires {Arity} argument, but received {arguments.Count}.", nameof(arguments));
        }

        return new TextValue(arguments[0].TypeName);
    }
}
