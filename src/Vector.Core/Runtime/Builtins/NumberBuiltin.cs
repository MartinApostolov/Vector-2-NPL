using System.Globalization;
using Vector.Core.Diagnostics;
using Vector.Core.Runtime.Values;

namespace Vector.Core.Runtime.Builtins;

public sealed class NumberBuiltin : BuiltinFunction
{
    public override string Name => "number";

    public override int Arity => 1;

    public override VectorValue Call(Interpreter interpreter, IReadOnlyList<VectorValue> arguments)
    {
        ArgumentNullException.ThrowIfNull(interpreter);
        ArgumentNullException.ThrowIfNull(arguments);

        if (arguments.Count != Arity)
        {
            throw new ArgumentException($"Builtin '{Name}' requires {Arity} argument, but received {arguments.Count}.", nameof(arguments));
        }

        if (arguments[0] is NumberValue number)
        {
            return number;
        }

        if (arguments[0] is TextValue text
            && double.TryParse(text.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed)
            && double.IsFinite(parsed))
        {
            return new NumberValue(parsed);
        }

        if (arguments[0] is TextValue invalidText)
        {
            throw RuntimeFailure(
                DiagnosticCode.RuntimeTypeError,
                $"number(value) could not convert text '{invalidText.Value}' to a number.");
        }

        throw RuntimeFailure(
            DiagnosticCode.RuntimeTypeError,
            $"number(value) requires a number or numeric text, but received {arguments[0].TypeName}.");
    }
}
