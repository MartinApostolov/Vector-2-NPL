using Vector.Core.Diagnostics;
using Vector.Core.Runtime.Values;

namespace Vector.Core.Runtime.Builtins;

/// <summary>
/// Creates an ascending integer list from start (inclusive) to end (exclusive).
/// </summary>
public sealed class RangeBuiltin : BuiltinFunction
{
    public override string Name => "range";

    public override int Arity => 2;

    public override VectorValue Call(Interpreter interpreter, IReadOnlyList<VectorValue> arguments)
    {
        ArgumentNullException.ThrowIfNull(interpreter);
        ArgumentNullException.ThrowIfNull(arguments);

        if (arguments.Count != Arity)
        {
            throw new ArgumentException($"Builtin '{Name}' requires {Arity} arguments, but received {arguments.Count}.", nameof(arguments));
        }

        var start = RequireWholeNumber(arguments[0], "start");
        var end = RequireWholeNumber(arguments[1], "end");

        if (start >= end)
        {
            return new ListValue();
        }

        var values = new List<VectorValue>();
        for (var value = start; value < end; value++)
        {
            values.Add(new NumberValue(value));

            if (value == long.MaxValue)
            {
                break;
            }
        }

        return new ListValue(values);
    }

    private static long RequireWholeNumber(VectorValue value, string parameterName)
    {
        if (value is NumberValue number
            && double.IsFinite(number.Value)
            && number.Value == Math.Truncate(number.Value)
            && number.Value >= long.MinValue
            && number.Value <= long.MaxValue)
        {
            return (long)number.Value;
        }

        throw RuntimeFailure(
            DiagnosticCode.RuntimeTypeError,
            $"range(start, end) requires finite whole-number bounds; {parameterName} was {value.TypeName}." );
    }
}
