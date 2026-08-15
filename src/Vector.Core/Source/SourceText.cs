namespace Vector.Core.Source;

/// <summary>
/// Owns source text and provides consistent offset-to-line/column mapping.
/// </summary>
public sealed class SourceText
{
    private readonly int[] _lineStarts;

    public SourceText(string text)
    {
        Text = text ?? throw new ArgumentNullException(nameof(text));
        _lineStarts = BuildLineStarts(text);
    }

    public string Text { get; }

    public int Length => Text.Length;

    public int LineCount => _lineStarts.Length;

    public char this[int offset] => Text[offset];

    public SourcePosition GetPosition(int offset)
    {
        if (offset < 0 || offset > Length)
        {
            throw new ArgumentOutOfRangeException(nameof(offset));
        }

        var lineIndex = Array.BinarySearch(_lineStarts, offset);
        if (lineIndex < 0)
        {
            lineIndex = ~lineIndex - 1;
        }

        return new SourcePosition(
            offset,
            lineIndex + 1,
            offset - _lineStarts[lineIndex] + 1);
    }

    public SourceSpan GetSpan(int startOffset, int endOffset)
    {
        if (startOffset < 0 || startOffset > Length)
        {
            throw new ArgumentOutOfRangeException(nameof(startOffset));
        }

        if (endOffset < startOffset || endOffset > Length)
        {
            throw new ArgumentOutOfRangeException(nameof(endOffset));
        }

        return new SourceSpan(GetPosition(startOffset), GetPosition(endOffset));
    }

    private static int[] BuildLineStarts(string text)
    {
        var starts = new List<int> { 0 };

        for (var offset = 0; offset < text.Length; offset++)
        {
            if (text[offset] == '\r')
            {
                if (offset + 1 < text.Length && text[offset + 1] == '\n')
                {
                    offset++;
                }

                starts.Add(offset + 1);
            }
            else if (text[offset] == '\n')
            {
                starts.Add(offset + 1);
            }
        }

        return starts.ToArray();
    }
}
