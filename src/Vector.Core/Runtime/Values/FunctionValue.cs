using System.Runtime.CompilerServices;

namespace Vector.Core.Runtime.Values;

public abstract class FunctionValue : VectorValue
{
    public sealed override VectorValueKind Kind => VectorValueKind.Function;

    public sealed override bool Equals(VectorValue? other) => ReferenceEquals(this, other);

    public sealed override int GetHashCode() => RuntimeHelpers.GetHashCode(this);
}
