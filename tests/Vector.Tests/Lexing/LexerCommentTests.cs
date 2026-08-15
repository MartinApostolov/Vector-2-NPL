using Vector.Core.Diagnostics;
using Vector.Core.Lexing;
using Vector.Core.Source;
using Xunit;

namespace Vector.Tests.Lexing;

public sealed class LexerCommentTests
{
    [Fact]
    public void Lexer_SkipsLineCommentAndContinuesOnNextLine()
    {
        var lexer = CreateLexer("first // ignored\r\nsecond");

        var first = lexer.Lex();
        var second = lexer.Lex();

        Assert.Equal(TokenKind.Identifier, first.Kind);
        Assert.Equal(TokenKind.Identifier, second.Kind);
        Assert.Equal("second", second.Value);
        Assert.Equal(new SourcePosition(18, 2, 1), second.Span.Start);
        Assert.Equal(TokenKind.EndOfFile, lexer.Lex().Kind);
        Assert.Empty(lexer.Diagnostics);
    }

    [Fact]
    public void Lexer_LineCommentAtEndOfFileProducesNoTokenOrDiagnostic()
    {
        var lexer = CreateLexer("value // comment");

        Assert.Equal(TokenKind.Identifier, lexer.Lex().Kind);
        Assert.Equal(TokenKind.EndOfFile, lexer.Lex().Kind);
        Assert.Empty(lexer.Diagnostics);
    }

    [Fact]
    public void Lexer_SkipsBlockCommentBetweenTokens()
    {
        var lexer = CreateLexer("left/* comment */+right");

        Assert.Equal(TokenKind.Identifier, lexer.Lex().Kind);
        Assert.Equal(TokenKind.Plus, lexer.Lex().Kind);
        Assert.Equal(TokenKind.Identifier, lexer.Lex().Kind);
        Assert.Equal(TokenKind.EndOfFile, lexer.Lex().Kind);
        Assert.Empty(lexer.Diagnostics);
    }

    [Fact]
    public void Lexer_BlockCommentMayContainNewlinesUnicodeAndOperators()
    {
        var lexer = CreateLexer("before/* Здравей\n+ - == \"text\" */after");

        Assert.Equal("before", lexer.Lex().Value);
        var after = lexer.Lex();

        Assert.Equal(TokenKind.Identifier, after.Kind);
        Assert.Equal("after", after.Value);
        Assert.Equal(new SourcePosition(33, 2, 17), after.Span.Start);
        Assert.Equal(TokenKind.EndOfFile, lexer.Lex().Kind);
        Assert.Empty(lexer.Diagnostics);
    }

    [Fact]
    public void Lexer_EmptyCommentsAreSkipped()
    {
        var lexer = CreateLexer("a/**/b//\n c");

        Assert.Equal("a", lexer.Lex().Value);
        Assert.Equal("b", lexer.Lex().Value);
        Assert.Equal("c", lexer.Lex().Value);
        Assert.Equal(TokenKind.EndOfFile, lexer.Lex().Kind);
        Assert.Empty(lexer.Diagnostics);
    }

    [Fact]
    public void Lexer_SlashStillProducesOperatorWhenNotStartingComment()
    {
        var lexer = CreateLexer("a / b");

        Assert.Equal(TokenKind.Identifier, lexer.Lex().Kind);
        Assert.Equal(TokenKind.Slash, lexer.Lex().Kind);
        Assert.Equal(TokenKind.Identifier, lexer.Lex().Kind);
        Assert.Equal(TokenKind.EndOfFile, lexer.Lex().Kind);
        Assert.Empty(lexer.Diagnostics);
    }

    [Fact]
    public void Lexer_ReportsUnterminatedBlockCommentAndThenEndOfFile()
    {
        const string source = "value /* never closes";
        var lexer = CreateLexer(source);

        Assert.Equal(TokenKind.Identifier, lexer.Lex().Kind);
        var eof = lexer.Lex();

        Assert.Equal(TokenKind.EndOfFile, eof.Kind);
        var diagnostic = Assert.Single(lexer.Diagnostics);
        Assert.Equal(DiagnosticCode.UnterminatedBlockComment, diagnostic.Code);
        Assert.Equal(DiagnosticSeverity.Error, diagnostic.Severity);
        Assert.Equal("/* never closes", source[diagnostic.Span.Start.Offset..diagnostic.Span.End.Offset]);
    }

    [Fact]
    public void Lexer_BlockCommentsDoNotNest()
    {
        var lexer = CreateLexer("a /* outer /* inner */ end */ b");

        Assert.Equal("a", lexer.Lex().Value);
        var end = lexer.Lex();
        var star = lexer.Lex();
        var slash = lexer.Lex();
        var b = lexer.Lex();

        Assert.Equal(TokenKind.Identifier, end.Kind);
        Assert.Equal("end", end.Value);
        Assert.Equal(TokenKind.Star, star.Kind);
        Assert.Equal(TokenKind.Slash, slash.Kind);
        Assert.Equal("b", b.Value);
        Assert.Equal(TokenKind.EndOfFile, lexer.Lex().Kind);
        Assert.Empty(lexer.Diagnostics);
    }

    private static Lexer CreateLexer(string text) => new(new SourceText(text));
}
