namespace Vector.Core.Runtime.Values;

public sealed class NumberValue : VectorValue
{
    public NumberValue(double value)
    {
        Value = value;
    }

    public double Value { get; }

    public override VectorValueKind Kind => VectorValueKind.Number;

    public override bool Equals(VectorValue? other) =>
        other is NumberValue number && Value.Equals(number.Value);

    public override int GetHashCode() => HashCode.Combine(Kind, Value);
}
