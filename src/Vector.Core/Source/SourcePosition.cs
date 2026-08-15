namespace Vector.Core.Source;

/// <summary>
/// Identifies an exact position in source text.
/// Offsets are zero-based; lines and columns are one-based.
/// </summary>
public readonly record struct SourcePosition
{
    public SourcePosition(int offset, int line, int column)
    {
        if (offset < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(offset));
        }

        if (line < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(line));
        }

        if (column < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(column));
        }

        Offset = offset;
        Line = line;
        Column = column;
    }

    public int Offset { get; }

    public int Line { get; }

    public int Column { get; }
}
