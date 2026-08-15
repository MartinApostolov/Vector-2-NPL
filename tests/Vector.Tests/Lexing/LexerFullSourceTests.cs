using Vector.Core.Lexing;
using Vector.Core.Source;
using Xunit;

namespace Vector.Tests.Lexing;

public sealed class LexerFullSourceTests
{
    [Fact]
    public void Lexer_LexesRepresentativeSourceWithCommentsAndUnicodeNames()
    {
        const string source = """
            let здраве2 = 10;
            // lower health
            if здраве2 >= 5 {
                /* one step */
                здраве2 = здраве2 - 1;
            }
            """;
        var lexer = CreateLexer(source);

        var kinds = LexAll(lexer).Select(token => token.Kind).ToArray();

        Assert.Equal(
            new[]
            {
                TokenKind.LetKeyword,
                TokenKind.Identifier,
                TokenKind.Equals,
                TokenKind.Number,
                TokenKind.Semicolon,
                TokenKind.IfKeyword,
                TokenKind.Identifier,
                TokenKind.GreaterOrEqual,
                TokenKind.Number,
                TokenKind.OpenBrace,
                TokenKind.Identifier,
                TokenKind.Equals,
                TokenKind.Identifier,
                TokenKind.Minus,
                TokenKind.Number,
                TokenKind.Semicolon,
                TokenKind.CloseBrace,
                TokenKind.EndOfFile
            },
            kinds);
        Assert.Empty(lexer.Diagnostics);
    }

    [Fact]
    public void Lexer_PreservesLiteralValuesAcrossCommentBoundaries()
    {
        var lexer = CreateLexer("let име = /*label*/ \"Вектор\\nезик\"; let x = 2.5e2;//done");
        var tokens = LexAll(lexer);

        var name = tokens.Single(token => token.Text == "име");
        var text = tokens.Single(token => token.Kind == TokenKind.String);
        var number = tokens.Single(token => token.Kind == TokenKind.Number);

        Assert.Equal("име", name.Value);
        Assert.Equal("Вектор\nезик", text.Value);
        Assert.Equal(250d, number.Value);
        Assert.Empty(lexer.Diagnostics);
    }

    [Fact]
    public void Lexer_TracksTokenPositionAfterMultilineBlockComment()
    {
        var lexer = CreateLexer("first /* one\r\ntwo */ second");

        var first = lexer.Lex();
        var second = lexer.Lex();

        Assert.Equal(new SourcePosition(0, 1, 1), first.Span.Start);
        Assert.Equal(new SourcePosition(21, 2, 8), second.Span.Start);
        Assert.Equal("second", second.Value);
        Assert.Empty(lexer.Diagnostics);
    }

    [Fact]
    public void Lexer_MixesCommentsWithEveryLiteralCategoryAvailableSoFar()
    {
        var lexer = CreateLexer("true /*a*/ false //b\nnothing 12 \"text\" + _име");
        var tokens = LexAll(lexer);

        Assert.Equal(TokenKind.TrueKeyword, tokens[0].Kind);
        Assert.Equal(TokenKind.FalseKeyword, tokens[1].Kind);
        Assert.Equal(TokenKind.NothingKeyword, tokens[2].Kind);
        Assert.Equal(TokenKind.Number, tokens[3].Kind);
        Assert.Equal(TokenKind.String, tokens[4].Kind);
        Assert.Equal(TokenKind.Plus, tokens[5].Kind);
        Assert.Equal(TokenKind.Identifier, tokens[6].Kind);
        Assert.Equal(TokenKind.EndOfFile, tokens[7].Kind);
        Assert.Empty(lexer.Diagnostics);
    }

    private static Lexer CreateLexer(string text) => new(new SourceText(text));

    private static List<Token> LexAll(Lexer lexer)
    {
        var tokens = new List<Token>();
        Token token;

        do
        {
            token = lexer.Lex();
            tokens.Add(token);
        }
        while (token.Kind != TokenKind.EndOfFile);

        return tokens;
    }
}
