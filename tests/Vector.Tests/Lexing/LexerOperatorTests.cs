using Vector.Core.Lexing;
using Vector.Core.Source;
using Xunit;

namespace Vector.Tests.Lexing;

public sealed class LexerOperatorTests
{
    public static TheoryData<string, TokenKind> OperatorsAndPunctuation => new()
    {
        { "+", TokenKind.Plus },
        { "-", TokenKind.Minus },
        { "*", TokenKind.Star },
        { "/", TokenKind.Slash },
        { "%", TokenKind.Percent },
        { "<", TokenKind.Less },
        { "<=", TokenKind.LessOrEqual },
        { ">", TokenKind.Greater },
        { ">=", TokenKind.GreaterOrEqual },
        { "==", TokenKind.EqualEqual },
        { "!=", TokenKind.BangEqual },
        { "=", TokenKind.Equals },
        { "(", TokenKind.OpenParen },
        { ")", TokenKind.CloseParen },
        { "{", TokenKind.OpenBrace },
        { "}", TokenKind.CloseBrace },
        { "[", TokenKind.OpenBracket },
        { "]", TokenKind.CloseBracket },
        { ",", TokenKind.Comma },
        { ";", TokenKind.Semicolon },
        { ".", TokenKind.Dot }
    };

    [Theory]
    [MemberData(nameof(OperatorsAndPunctuation))]
    public void Lexer_LexesOperatorOrPunctuation(string text, TokenKind expected)
    {
        var lexer = new Lexer(new SourceText(text));

        var token = lexer.Lex();

        Assert.Equal(expected, token.Kind);
        Assert.Equal(text, token.Text);
        Assert.Null(token.Value);
        Assert.Equal(TokenKind.EndOfFile, lexer.Lex().Kind);
        Assert.Empty(lexer.Diagnostics);
    }

    [Fact]
    public void Lexer_PrefersTwoCharacterOperators()
    {
        var lexer = new Lexer(new SourceText("<= >= == !="));

        Assert.Equal(TokenKind.LessOrEqual, lexer.Lex().Kind);
        Assert.Equal(TokenKind.GreaterOrEqual, lexer.Lex().Kind);
        Assert.Equal(TokenKind.EqualEqual, lexer.Lex().Kind);
        Assert.Equal(TokenKind.BangEqual, lexer.Lex().Kind);
        Assert.Equal(TokenKind.EndOfFile, lexer.Lex().Kind);
        Assert.Empty(lexer.Diagnostics);
    }

    [Fact]
    public void Lexer_TracksOperatorSpans()
    {
        var lexer = new Lexer(new SourceText("x <= y"));

        lexer.Lex();
        var operation = lexer.Lex();

        Assert.Equal(TokenKind.LessOrEqual, operation.Kind);
        Assert.Equal(new SourcePosition(2, 1, 3), operation.Span.Start);
        Assert.Equal(new SourcePosition(4, 1, 5), operation.Span.End);
    }
}
