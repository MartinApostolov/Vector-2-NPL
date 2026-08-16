using Vector.Core.Diagnostics;
using Vector.Core.Runtime.Values;

namespace Vector.Core.Runtime.Builtins;

public sealed class ConcatBuiltin : BuiltinFunction
{
    public override string Name => "concat";

    public override int Arity => 2;

    public override VectorValue Call(Interpreter interpreter, IReadOnlyList<VectorValue> arguments)
    {
        ArgumentNullException.ThrowIfNull(interpreter);
        ArgumentNullException.ThrowIfNull(arguments);

        if (arguments.Count != Arity)
        {
            throw new ArgumentException($"Builtin '{Name}' requires {Arity} arguments, but received {arguments.Count}.", nameof(arguments));
        }

        if (arguments[0] is not ListValue left || arguments[1] is not ListValue right)
        {
            throw RuntimeFailure(
                DiagnosticCode.RuntimeTypeError,
                $"concat(listA, listB) requires two lists, but received {arguments[0].TypeName} and {arguments[1].TypeName}.");
        }

        return new ListValue(left.Elements.Concat(right.Elements));
    }
}
