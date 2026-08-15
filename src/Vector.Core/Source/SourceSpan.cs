namespace Vector.Core.Source;

/// <summary>
/// Represents a half-open source range from <see cref="Start"/> (inclusive)
/// to <see cref="End"/> (exclusive).
/// </summary>
public readonly record struct SourceSpan
{
    public SourceSpan(SourcePosition start, SourcePosition end)
    {
        if (end.Offset < start.Offset)
        {
            throw new ArgumentException("The end of a source span cannot precede its start.", nameof(end));
        }

        Start = start;
        End = end;
    }

    public SourcePosition Start { get; }

    public SourcePosition End { get; }

    public int Length => End.Offset - Start.Offset;
}
