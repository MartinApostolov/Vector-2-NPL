namespace Vector.Core.Runtime.Values;

public sealed class ListValue : VectorValue
{
    private readonly List<VectorValue> _elements;

    public ListValue()
        : this(Array.Empty<VectorValue>())
    {
    }

    public ListValue(IEnumerable<VectorValue> elements)
    {
        ArgumentNullException.ThrowIfNull(elements);
        _elements = elements.Select(RequireValue).ToList();
    }

    public override VectorValueKind Kind => VectorValueKind.List;

    public int Count => _elements.Count;

    public IReadOnlyList<VectorValue> Elements => _elements;

    public VectorValue this[int index]
    {
        get => _elements[index];
        set => _elements[index] = RequireValue(value);
    }

    public bool IsNumericList => _elements.All(element => element.Kind == VectorValueKind.Number);

    /// <summary>
    /// Returns true when placing <paramref name="value"/> inside this list would make
    /// this list directly or indirectly contain itself.
    /// </summary>
    public bool WouldCreateCycle(VectorValue value)
    {
        ArgumentNullException.ThrowIfNull(value);

        return value is ListValue list
            && ContainsReference(list, this, new HashSet<ListValue>(ReferenceEqualityComparer.Instance));
    }

    public override bool Equals(VectorValue? other)
    {
        if (ReferenceEquals(this, other))
        {
            return true;
        }

        if (other is not ListValue list || Count != list.Count)
        {
            return false;
        }

        for (var i = 0; i < Count; i++)
        {
            if (!_elements[i].Equals(list._elements[i]))
            {
                return false;
            }
        }

        return true;
    }

    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(Kind);

        foreach (var element in _elements)
        {
            hash.Add(element);
        }

        return hash.ToHashCode();
    }

    private static bool ContainsReference(
        ListValue current,
        ListValue target,
        HashSet<ListValue> visited)
    {
        if (ReferenceEquals(current, target))
        {
            return true;
        }

        if (!visited.Add(current))
        {
            return false;
        }

        foreach (var element in current._elements)
        {
            if (element is ListValue nested && ContainsReference(nested, target, visited))
            {
                return true;
            }
        }

        return false;
    }

    private static VectorValue RequireValue(VectorValue? value) =>
        value ?? throw new ArgumentNullException(nameof(value), "Vector lists cannot contain C# null; use NothingValue.Instance for Vector 'nothing'.");
}
