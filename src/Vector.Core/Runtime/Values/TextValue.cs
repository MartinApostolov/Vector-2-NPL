namespace Vector.Core.Runtime.Values;

public sealed class TextValue : VectorValue
{
    public TextValue(string value)
    {
        Value = value ?? throw new ArgumentNullException(nameof(value));
    }

    public string Value { get; }

    public override VectorValueKind Kind => VectorValueKind.Text;

    public override bool Equals(VectorValue? other) =>
        other is TextValue text && string.Equals(Value, text.Value, StringComparison.Ordinal);

    public override int GetHashCode() => HashCode.Combine(Kind, StringComparer.Ordinal.GetHashCode(Value));
}
