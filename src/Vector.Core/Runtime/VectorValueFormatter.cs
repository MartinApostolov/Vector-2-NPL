using System.Globalization;
using System.Text;
using Vector.Core.Runtime.Values;

namespace Vector.Core.Runtime;

/// <summary>
/// Formats Vector runtime values consistently for user-visible output.
/// </summary>
public static class VectorValueFormatter
{
    public static string Format(VectorValue value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return FormatValue(value, quoteText: false);
    }

    private static string FormatValue(VectorValue value, bool quoteText)
    {
        return value switch
        {
            NumberValue number => number.Value.ToString("G", CultureInfo.InvariantCulture),
            TextValue text => quoteText ? QuoteText(text.Value) : text.Value,
            BooleanValue boolean => boolean.Value ? "true" : "false",
            NothingValue => "nothing",
            ListValue list => $"[{string.Join(", ", list.Elements.Select(element => FormatValue(element, quoteText: true)))}]",
            FunctionValue => "<function>",
            _ => throw new InvalidOperationException(
                $"Cannot format Vector value type '{value.GetType().Name}'.")
        };
    }

    private static string QuoteText(string value)
    {
        var builder = new StringBuilder(value.Length + 2);
        builder.Append('"');

        foreach (var character in value)
        {
            builder.Append(character switch
            {
                '"' => "\\\"",
                '\\' => "\\\\",
                '\n' => "\\n",
                '\r' => "\\r",
                '\t' => "\\t",
                _ => character.ToString()
            });
        }

        builder.Append('"');
        return builder.ToString();
    }
}
