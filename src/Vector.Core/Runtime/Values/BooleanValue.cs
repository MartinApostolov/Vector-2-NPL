namespace Vector.Core.Runtime.Values;

public sealed class BooleanValue : VectorValue
{
    public BooleanValue(bool value)
    {
        Value = value;
    }

    public bool Value { get; }

    public override VectorValueKind Kind => VectorValueKind.Boolean;

    public override bool Equals(VectorValue? other) =>
        other is BooleanValue boolean && Value == boolean.Value;

    public override int GetHashCode() => HashCode.Combine(Kind, Value);
}
