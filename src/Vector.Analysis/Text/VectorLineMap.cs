namespace Vector.Analysis.Text;

using Vector.Core.Source;

public sealed class VectorLineMap
{
    private readonly string text;
    private readonly int[] lineStarts;

    public VectorLineMap(string text)
    {
        this.text = text ?? throw new ArgumentNullException(nameof(text));
        this.lineStarts = BuildLineStarts(text);
    }

    public int LineCount => this.lineStarts.Length;

    public TextPosition GetPosition(int utf16Offset)
    {
        if (utf16Offset < 0 || utf16Offset > this.text.Length)
        {
            throw new ArgumentOutOfRangeException(nameof(utf16Offset));
        }

        int line = Array.BinarySearch(this.lineStarts, utf16Offset);
        if (line < 0)
        {
            line = ~line - 1;
        }

        return new TextPosition(line, utf16Offset - this.lineStarts[line]);
    }

    public int GetOffset(TextPosition position)
    {
        if (position.Line >= this.lineStarts.Length)
        {
            throw new ArgumentOutOfRangeException(nameof(position));
        }

        int lineStart = this.lineStarts[position.Line];
        int lineEnd = GetLineContentEnd(position.Line);
        int offset = lineStart + position.Character;
        if (offset > lineEnd)
        {
            throw new ArgumentOutOfRangeException(nameof(position));
        }

        return offset;
    }

    public TextRange GetRange(SourceSpan span) => new(GetPosition(span.Start.Offset), GetPosition(span.End.Offset));

    public SourceSpan GetSpan(TextRange range)
    {
        var source = new SourceText(this.text);
        return source.GetSpan(GetOffset(range.Start), GetOffset(range.End));
    }

    private int GetLineContentEnd(int line)
    {
        int end = line + 1 < this.lineStarts.Length ? this.lineStarts[line + 1] : this.text.Length;
        if (end > 0 && this.text[end - 1] == '\n')
        {
            end--;
        }

        if (end > 0 && this.text[end - 1] == '\r')
        {
            end--;
        }

        return end;
    }

    private static int[] BuildLineStarts(string text)
    {
        var starts = new List<int> { 0 };
        for (int offset = 0; offset < text.Length; offset++)
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
