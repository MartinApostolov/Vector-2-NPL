namespace Vector.Analysis.Text;

public readonly record struct TextRange
{
    public TextRange(TextPosition start, TextPosition end)
    {
        if (end.Line < start.Line || (end.Line == start.Line && end.Character < start.Character))
        {
            throw new ArgumentException("The end of a text range cannot precede its start.", nameof(end));
        }

        this.Start = start;
        this.End = end;
    }

    public TextPosition Start { get; }

    public TextPosition End { get; }
}
