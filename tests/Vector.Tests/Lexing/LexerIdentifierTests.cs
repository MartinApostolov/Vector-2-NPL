using System.Text;
using Vector.Core.Diagnostics;
using Vector.Core.Lexing;
using Vector.Core.Source;
using Xunit;

namespace Vector.Tests.Lexing;

public sealed class LexerIdentifierTests
{
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
    public void Lexer_LexesIdentifierAndKeywordSequence()
    {
        var lexer = CreateLexer("let health = value;");

        Assert.Equal(TokenKind.LetKeyword, lexer.Lex().Kind);

        var name = lexer.Lex();
        Assert.Equal(TokenKind.Identifier, name.Kind);
        Assert.Equal("health", name.Text);
        Assert.Equal("health", name.Value);

        Assert.Equal(TokenKind.Equals, lexer.Lex().Kind);
        Assert.Equal(TokenKind.Identifier, lexer.Lex().Kind);
        Assert.Equal(TokenKind.Semicolon, lexer.Lex().Kind);
        Assert.Equal(TokenKind.EndOfFile, lexer.Lex().Kind);
        Assert.Empty(lexer.Diagnostics);
    }

    [Theory]
    [MemberData(nameof(Keywords))]
    public void Lexer_LexesEveryReservedKeyword(string text, TokenKind expected)
    {
        var lexer = CreateLexer(text);

        var token = lexer.Lex();

        Assert.Equal(expected, token.Kind);
        Assert.Equal(text, token.Text);
        Assert.Null(token.Value);
        Assert.Equal(TokenKind.EndOfFile, lexer.Lex().Kind);
        Assert.Empty(lexer.Diagnostics);
    }

    [Fact]
    public void Lexer_LexesUnicodeIdentifiersAndDigitsAfterFirstCharacter()
    {
        var lexer = CreateLexer("здраве2 _temporary playerHealth");

        AssertIdentifier(lexer.Lex(), "здраве2");
        AssertIdentifier(lexer.Lex(), "_temporary");
        AssertIdentifier(lexer.Lex(), "playerHealth");
        Assert.Equal(TokenKind.EndOfFile, lexer.Lex().Kind);
        Assert.Empty(lexer.Diagnostics);
    }

    [Fact]
    public void Lexer_NormalizesIdentifierValuesToNfcWhilePreservingSourceText()
    {
        var decomposed = "cafe\u0301";
        var lexer = CreateLexer(decomposed);

        var token = lexer.Lex();

        Assert.Equal(TokenKind.Identifier, token.Kind);
        Assert.Equal(decomposed, token.Text);

        var normalizedValue = Assert.IsType<string>(token.Value);
        Assert.Equal(decomposed.Normalize(NormalizationForm.FormC), normalizedValue);
        Assert.Equal(5, token.Text.Length);
        Assert.Equal(4, normalizedValue.Length);
        Assert.False(string.Equals(token.Text, normalizedValue, StringComparison.Ordinal));
    }

    [Fact]
    public void Lexer_SkipsWhitespaceAndTracksCrLfPositions()
    {
        var lexer = CreateLexer("first\r\n  second");

        var first = lexer.Lex();
        var second = lexer.Lex();

        Assert.Equal(new SourcePosition(0, 1, 1), first.Span.Start);
        Assert.Equal(new SourcePosition(5, 1, 6), first.Span.End);
        Assert.Equal(new SourcePosition(9, 2, 3), second.Span.Start);
        Assert.Equal(new SourcePosition(15, 2, 9), second.Span.End);
    }

    [Fact]
    public void Lexer_ReportsInvalidCharacterAndContinues()
    {
        var lexer = CreateLexer("alpha @ beta");

        Assert.Equal(TokenKind.Identifier, lexer.Lex().Kind);
        var bad = lexer.Lex();
        Assert.Equal(TokenKind.Identifier, lexer.Lex().Kind);
        Assert.Equal(TokenKind.EndOfFile, lexer.Lex().Kind);

        Assert.Equal(TokenKind.BadToken, bad.Kind);
        Assert.Equal("@", bad.Text);

        var diagnostic = Assert.Single(lexer.Diagnostics);
        Assert.Equal(DiagnosticCode.InvalidCharacter, diagnostic.Code);
        Assert.Equal(DiagnosticSeverity.Error, diagnostic.Severity);
        Assert.Equal(bad.Span, diagnostic.Span);
        Assert.Contains("@", diagnostic.Message);
    }

    [Fact]
    public void Lexer_EmptySourceProducesOnlyEndOfFile()
    {
        var lexer = CreateLexer(string.Empty);

        var token = lexer.Lex();

        Assert.Equal(TokenKind.EndOfFile, token.Kind);
        Assert.Equal(string.Empty, token.Text);
        Assert.Equal(0, token.Span.Length);
        Assert.Empty(lexer.Diagnostics);
    }

    private static Lexer CreateLexer(string text) => new(new SourceText(text));

    private static void AssertIdentifier(Token token, string expected)
    {
        Assert.Equal(TokenKind.Identifier, token.Kind);
        Assert.Equal(expected, token.Text);
        Assert.Equal(expected.Normalize(NormalizationForm.FormC), token.Value);
    }
}
