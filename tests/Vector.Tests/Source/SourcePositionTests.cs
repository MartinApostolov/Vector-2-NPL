using Vector.Core.Source;
using Xunit;

namespace Vector.Tests.Source;

public sealed class SourcePositionTests
{
    [Fact]
    public void SourcePosition_StoresOffsetLineAndColumn()
    {
        var position = new SourcePosition(12, 3, 5);

        Assert.Equal(12, position.Offset);
        Assert.Equal(3, position.Line);
        Assert.Equal(5, position.Column);
    }

    [Theory]
    [InlineData(-1, 1, 1)]
    [InlineData(0, 0, 1)]
    [InlineData(0, 1, 0)]
    public void SourcePosition_RejectsInvalidCoordinates(int offset, int line, int column)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new SourcePosition(offset, line, column));
    }

    [Fact]
    public void SourceText_EmptyTextHasOneLineAndOnePosition()
    {
        var source = new SourceText(string.Empty);

        Assert.Equal(0, source.Length);
        Assert.Equal(1, source.LineCount);
        Assert.Equal(new SourcePosition(0, 1, 1), source.GetPosition(0));
    }

    [Fact]
    public void SourceText_MapsOffsetsAcrossLfNewlines()
    {
        var source = new SourceText("ab\ncd");

        Assert.Equal(new SourcePosition(0, 1, 1), source.GetPosition(0));
        Assert.Equal(new SourcePosition(2, 1, 3), source.GetPosition(2));
        Assert.Equal(new SourcePosition(3, 2, 1), source.GetPosition(3));
        Assert.Equal(new SourcePosition(5, 2, 3), source.GetPosition(5));
        Assert.Equal(2, source.LineCount);
    }

    [Fact]
    public void SourceText_TreatsCrLfAsSingleNewline()
    {
        var source = new SourceText("ab\r\ncd");

        Assert.Equal(new SourcePosition(2, 1, 3), source.GetPosition(2));
        Assert.Equal(new SourcePosition(3, 1, 4), source.GetPosition(3));
        Assert.Equal(new SourcePosition(4, 2, 1), source.GetPosition(4));
        Assert.Equal(new SourcePosition(6, 2, 3), source.GetPosition(6));
        Assert.Equal(2, source.LineCount);
    }

    [Fact]
    public void SourceText_TracksTrailingEmptyLine()
    {
        var source = new SourceText("first\n");

        Assert.Equal(2, source.LineCount);
        Assert.Equal(new SourcePosition(6, 2, 1), source.GetPosition(source.Length));
    }

    [Fact]
    public void SourceText_GetSpanUsesEndExclusiveOffsets()
    {
        var source = new SourceText("abc\ndef");

        var span = source.GetSpan(1, 5);

        Assert.Equal(new SourcePosition(1, 1, 2), span.Start);
        Assert.Equal(new SourcePosition(5, 2, 2), span.End);
        Assert.Equal(4, span.Length);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(4)]
    public void SourceText_GetPositionRejectsOutOfRangeOffsets(int offset)
    {
        var source = new SourceText("abc");

        Assert.Throws<ArgumentOutOfRangeException>(() => source.GetPosition(offset));
    }

    [Theory]
    [InlineData(-1, 0)]
    [InlineData(0, 4)]
    [InlineData(2, 1)]
    public void SourceText_GetSpanRejectsInvalidRanges(int startOffset, int endOffset)
    {
        var source = new SourceText("abc");

        Assert.Throws<ArgumentOutOfRangeException>(() => source.GetSpan(startOffset, endOffset));
    }

    [Fact]
    public void SourceSpan_RejectsEndBeforeStart()
    {
        var start = new SourcePosition(3, 1, 4);
        var end = new SourcePosition(2, 1, 3);

        Assert.Throws<ArgumentException>(() => new SourceSpan(start, end));
    }
}
