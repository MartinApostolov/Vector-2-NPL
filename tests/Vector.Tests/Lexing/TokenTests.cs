using Vector.Core.Lexing;
using Vector.Core.Source;
using Xunit;

namespace Vector.Tests.Lexing;

public sealed class TokenTests
{
    private static readonly SourceSpan TestSpan = new(
        new SourcePosition(4, 2, 1),
        new SourcePosition(7, 2, 4));

    public static TheoryData<string, TokenKind> Keywords => new()
    {
        { "let", TokenKind.LetKeyword },
        { "if", TokenKind.IfKeyword },
        { "else", TokenKind.ElseKeyword },
        { "while", TokenKind.WhileKeyword },
        { "for", TokenKind.ForKeyword },
        { "in", TokenKind.InKeyword },
        { "function", TokenKind.FunctionKeyword },
        { "return", TokenKind.ReturnKeyword },
        { "break", TokenKind.BreakKeyword },
        { "continue", TokenKind.ContinueKeyword },
        { "true", TokenKind.TrueKeyword },
        { "false", TokenKind.FalseKeyword },
        { "nothing", TokenKind.NothingKeyword },
        { "and", TokenKind.AndKeyword },
        { "or", TokenKind.OrKeyword },
        { "not", TokenKind.NotKeyword },
        { "import", TokenKind.ImportKeyword }
    };

    [Fact]
    public void Token_StoresKindTextValueAndSpan()
    {
        var token = new Token(TokenKind.Identifier, "abc", "abc", TestSpan);

        Assert.Equal(TokenKind.Identifier, token.Kind);
        Assert.Equal("abc", token.Text);
        Assert.Equal("abc", token.Value);
        Assert.Equal(TestSpan, token.Span);
    }

    [Fact]
    public void Token_AllowsEmptyTextForSyntheticTokens()
    {
        var position = new SourcePosition(0, 1, 1);
        var token = new Token(
            TokenKind.EndOfFile,
            string.Empty,
            null,
            new SourceSpan(position, position));

        Assert.Equal(string.Empty, token.Text);
        Assert.Null(token.Value);
        Assert.Equal(0, token.Span.Length);
    }

    [Fact]
    public void Token_RejectsNullText()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new Token(TokenKind.Identifier, null!, null, TestSpan));
    }

    [Theory]
    [MemberData(nameof(Keywords))]
    public void KeywordTable_RecognizesEveryReservedKeyword(string text, TokenKind expected)
    {
        Assert.Equal(expected, KeywordTable.GetKind(text));
    }

    [Theory]
    [InlineData("name")]
    [InlineData("Let")]
    [InlineData("IMPORT")]
    [InlineData("trueValue")]
    [InlineData("здраве")]
    public void KeywordTable_TreatsNonKeywordsAsIdentifiers(string text)
    {
        Assert.Equal(TokenKind.Identifier, KeywordTable.GetKind(text));
    }

    [Fact]
    public void KeywordTable_RejectsNullText()
    {
        Assert.Throws<ArgumentNullException>(() => KeywordTable.GetKind(null!));
    }

    [Fact]
    public void TokenKind_DefinesTheCompleteV1OperatorAndPunctuationVocabulary()
    {
        var expected = new[]
        {
            TokenKind.Plus,
            TokenKind.Minus,
            TokenKind.Star,
            TokenKind.Slash,
            TokenKind.Percent,
            TokenKind.Less,
            TokenKind.LessOrEqual,
            TokenKind.Greater,
            TokenKind.GreaterOrEqual,
            TokenKind.EqualEqual,
            TokenKind.BangEqual,
            TokenKind.Equals,
            TokenKind.OpenParen,
            TokenKind.CloseParen,
            TokenKind.OpenBrace,
            TokenKind.CloseBrace,
            TokenKind.OpenBracket,
            TokenKind.CloseBracket,
            TokenKind.Comma,
            TokenKind.Semicolon,
            TokenKind.Dot
        };

        Assert.Equal(21, expected.Distinct().Count());
    }
}
