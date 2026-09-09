namespace Vector.Analysis.Text;

public readonly record struct TextPosition
{
    public TextPosition(int line, int character)
    {
        if (line < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(line));
        }

        if (character < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(character));
        }

        this.Line = line;
        this.Character = character;
    }

    public int Line { get; }

    public int Character { get; }
}
