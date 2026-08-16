using Vector.Core.Diagnostics;
using Vector.Core.Runtime.Values;

namespace Vector.Core.Runtime.Native;

/// <summary>
/// Controlled conversions between Vector runtime values and the host types used by
/// explicitly registered native modules. This class intentionally does not provide a
/// general object/reflection conversion surface.
/// </summary>
public static class NativeValueConverter
{
    public static double ToNumber(VectorValue value, string? parameterName = null)
    {
        ArgumentNullException.ThrowIfNull(value);

        if (value is NumberValue number && double.IsFinite(number.Value))
        {
            return number.Value;
        }

        throw TypeFailure("a finite number", value, parameterName);
    }

    public static string ToText(VectorValue value, string? parameterName = null)
    {
        ArgumentNullException.ThrowIfNull(value);

        if (value is TextValue text)
        {
            return text.Value;
        }

        throw TypeFailure("text", value, parameterName);
    }

    public static bool ToBoolean(VectorValue value, string? parameterName = null)
    {
        ArgumentNullException.ThrowIfNull(value);

        if (value is BooleanValue boolean)
        {
            return boolean.Value;
        }

        throw TypeFailure("a boolean", value, parameterName);
    }

    public static IReadOnlyList<VectorValue> ToList(VectorValue value, string? parameterName = null)
    {
        ArgumentNullException.ThrowIfNull(value);

        if (value is ListValue list)
        {
            return list.Elements;
        }

        throw TypeFailure("a list", value, parameterName);
    }

    public static NothingValue ToNothing(VectorValue value, string? parameterName = null)
    {
        ArgumentNullException.ThrowIfNull(value);

        if (value is NothingValue nothing)
        {
            return nothing;
        }

        throw TypeFailure("nothing", value, parameterName);
    }

    public static string? ToNullableText(VectorValue value, string? parameterName = null) =>
        value is NothingValue ? null : ToText(value, parameterName);

    public static NumberValue FromNumber(double value)
    {
        if (!double.IsFinite(value))
        {
            throw new NativeRuntimeException(
                DiagnosticCode.NativeRuntimeFailure,
                "Native code attempted to return a non-finite number.");
        }

        return new NumberValue(value);
    }

    public static TextValue FromText(string value) =>
        new(value ?? throw new ArgumentNullException(nameof(value)));

    public static BooleanValue FromBoolean(bool value) => new(value);

    public static ListValue FromList(IEnumerable<VectorValue> values)
    {
        ArgumentNullException.ThrowIfNull(values);
        var list = new ListValue(values);
        ValidateOutboundValue(list);
        return list;
    }

    public static NothingValue FromNothing() => NothingValue.Instance;

    public static VectorValue FromNullableText(string? value) =>
        value is null ? NothingValue.Instance : new TextValue(value);

    internal static void ValidateOutboundValue(VectorValue value)
    {
        ArgumentNullException.ThrowIfNull(value);
        ValidateOutboundValue(value, new HashSet<ListValue>(ReferenceEqualityComparer.Instance));
    }

    private static void ValidateOutboundValue(VectorValue value, HashSet<ListValue> visited)
    {
        if (value is NumberValue number && !double.IsFinite(number.Value))
        {
            throw new NativeRuntimeException(
                DiagnosticCode.NativeRuntimeFailure,
                "Native code attempted to return a non-finite number.");
        }

        if (value is not ListValue list || !visited.Add(list))
        {
            return;
        }

        foreach (var element in list.Elements)
        {
            ValidateOutboundValue(element, visited);
        }
    }

    private static NativeRuntimeException TypeFailure(
        string expected,
        VectorValue actual,
        string? parameterName)
    {
        var subject = string.IsNullOrWhiteSpace(parameterName)
            ? "Native value"
            : $"Native argument '{parameterName}'";

        return new NativeRuntimeException(
            DiagnosticCode.RuntimeTypeError,
            $"{subject} must be {expected}, but received {actual.TypeName}.");
    }
}
