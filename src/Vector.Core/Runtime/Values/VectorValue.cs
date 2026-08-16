namespace Vector.Core.Runtime.Values;

public enum VectorValueKind
{
    Number,
    Text,
    Boolean,
    List,
    Function,
    Nothing
}

public abstract class VectorValue : IEquatable<VectorValue>
{
    public abstract VectorValueKind Kind { get; }

    public string TypeName => Kind switch
    {
        VectorValueKind.Number => "number",
        VectorValueKind.Text => "text",
        VectorValueKind.Boolean => "boolean",
        VectorValueKind.List => "list",
        VectorValueKind.Function => "function",
        VectorValueKind.Nothing => "nothing",
        _ => throw new InvalidOperationException($"Unknown Vector value kind '{Kind}'.")
    };

    public abstract bool Equals(VectorValue? other);

    public override bool Equals(object? obj) => obj is VectorValue other && Equals(other);

    public abstract override int GetHashCode();

    public static bool operator ==(VectorValue? left, VectorValue? right) =>
        ReferenceEquals(left, right) || (left is not null && left.Equals(right));

    public static bool operator !=(VectorValue? left, VectorValue? right) => !(left == right);
}
