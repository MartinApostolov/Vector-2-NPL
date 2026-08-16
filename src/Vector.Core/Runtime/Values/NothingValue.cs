namespace Vector.Core.Runtime.Values;

public sealed class NothingValue : VectorValue
{
    private NothingValue()
    {
    }

    public static NothingValue Instance { get; } = new();

    public override VectorValueKind Kind => VectorValueKind.Nothing;

    public override bool Equals(VectorValue? other) => other is NothingValue;

    public override int GetHashCode() => HashCode.Combine(Kind);
}
