using Vector.Core.Diagnostics;
using Vector.Core.Lexing;
using Vector.Core.Source;
using Xunit;

namespace Vector.Tests.Lexing;

public sealed class LexerLiteralTests
{
    public static TheoryData<string, double> ValidNumbers => new()
    {
        { "0", 0d },
        { "20", 20d },
        { "3.14", 3.14d },
        { "1000.5", 1000.5d },
        { "1e3", 1000d },
        { "2.5e-4", 0.00025d },
        { "6E+2", 600d }
    };

    public static TheoryData<string> MalformedNumbers => new()
    {
        ".5",
        "5.",
        "1e",
        "1e+",
        "2.5e-"
    };

    public static TheoryData<string, string> ValidStrings => new()
    {
        { "\"hello\"", "hello" },
        { "\"Здравей\"", "Здравей" },
        { "\"line\\nnext\"", "line\nnext" },
        { "\"return\\rnext\"", "return\rnext" },
        { "\"tab\\tnext\"", "tab\tnext" },
        { "\"quote: \\\"\"", "quote: \"" },
        { "\"" + "slash: " + "\\\\" + "\"", "slash: \\" },
        { "\"😀\"", "😀" }
    };

    [Theory]
    [MemberData(nameof(ValidNumbers))]
    public void Lexer_LexesValidNumberLiterals(string text, double expected)
    {
        var lexer = CreateLexer(text);

        var token = lexer.Lex();

        Assert.Equal(TokenKind.Number, token.Kind);
        Assert.Equal(text, token.Text);
        Assert.Equal(expected, Assert.IsType<double>(token.Value));
        Assert.Equal(TokenKind.EndOfFile, lexer.Lex().Kind);
        Assert.Empty(lexer.Diagnostics);
    }

    [Fact]
    public void Lexer_TreatsMinusAsSeparateUnaryOperator()
    {
        var lexer = CreateLexer("-5");

        Assert.Equal(TokenKind.Minus, lexer.Lex().Kind);
        var number = lexer.Lex();

        Assert.Equal(TokenKind.Number, number.Kind);
        Assert.Equal(5d, Assert.IsType<double>(number.Value));
        Assert.Equal(TokenKind.EndOfFile, lexer.Lex().Kind);
        Assert.Empty(lexer.Diagnostics);
    }

    [Fact]
    public void Lexer_NumberSpanCoversOnlyTheLiteral()
    {
        var lexer = CreateLexer("  12.5 + 1");

        var number = lexer.Lex();

        Assert.Equal(new SourcePosition(2, 1, 3), number.Span.Start);
        Assert.Equal(new SourcePosition(6, 1, 7), number.Span.End);
        Assert.Equal("12.5", number.Text);
    }

    [Theory]
    [MemberData(nameof(MalformedNumbers))]
    public void Lexer_ReportsMalformedNumbers(string text)
    {
        var lexer = CreateLexer(text);

        var token = lexer.Lex();

        Assert.Equal(TokenKind.BadToken, token.Kind);
        Assert.Equal(text, token.Text);
        Assert.Null(token.Value);
        var diagnostic = Assert.Single(lexer.Diagnostics);
        Assert.Equal(DiagnosticCode.MalformedNumber, diagnostic.Code);
        Assert.Equal(DiagnosticSeverity.Error, diagnostic.Severity);
        Assert.Equal(token.Span, diagnostic.Span);
        Assert.Equal(TokenKind.EndOfFile, lexer.Lex().Kind);
    }

    [Fact]
    public void Lexer_RejectsNumericOverflow()
    {
        var lexer = CreateLexer("1e9999");

        var token = lexer.Lex();

        Assert.Equal(TokenKind.BadToken, token.Kind);
        Assert.Equal(DiagnosticCode.MalformedNumber, Assert.Single(lexer.Diagnostics).Code);
    }

    [Theory]
    [MemberData(nameof(ValidStrings))]
    public void Lexer_LexesStringLiteralsAndDecodesEscapes(string text, string expected)
    {
        var lexer = CreateLexer(text);

        var token = lexer.Lex();

        Assert.Equal(TokenKind.String, token.Kind);
        Assert.Equal(text, token.Text);
        Assert.Equal(expected, Assert.IsType<string>(token.Value));
        Assert.Equal(TokenKind.EndOfFile, lexer.Lex().Kind);
        Assert.Empty(lexer.Diagnostics);
    }

    [Fact]
    public void Lexer_ReportsUnknownEscapeButContinuesToClosingQuote()
    {
        var lexer = CreateLexer("\"a\\qb\"");

        var token = lexer.Lex();

        Assert.Equal(TokenKind.String, token.Kind);
        Assert.Equal("aqb", Assert.IsType<string>(token.Value));
        var diagnostic = Assert.Single(lexer.Diagnostics);
        Assert.Equal(DiagnosticCode.InvalidEscapeSequence, diagnostic.Code);
        Assert.Equal(DiagnosticSeverity.Error, diagnostic.Severity);
        Assert.Equal("\\q", lexerText(diagnostic.Span, "\"a\\qb\""));
        Assert.Equal(TokenKind.EndOfFile, lexer.Lex().Kind);
    }

    [Fact]
    public void Lexer_ReportsUnterminatedStringAtEndOfFile()
    {
        var lexer = CreateLexer("\"hello");

        var token = lexer.Lex();

        Assert.Equal(TokenKind.BadToken, token.Kind);
        Assert.Equal("\"hello", token.Text);
        Assert.Null(token.Value);
        var diagnostic = Assert.Single(lexer.Diagnostics);
        Assert.Equal(DiagnosticCode.UnterminatedString, diagnostic.Code);
        Assert.Equal(token.Span, diagnostic.Span);
        Assert.Equal(TokenKind.EndOfFile, lexer.Lex().Kind);
    }

    [Fact]
    public void Lexer_ReportsUnterminatedStringAtNewlineAndRecovers()
    {
        var lexer = CreateLexer("\"hello\nnext");

        var token = lexer.Lex();
        var next = lexer.Lex();

        Assert.Equal(TokenKind.BadToken, token.Kind);
        Assert.Equal("\"hello", token.Text);
        Assert.Equal(DiagnosticCode.UnterminatedString, Assert.Single(lexer.Diagnostics).Code);
        Assert.Equal(TokenKind.Identifier, next.Kind);
        Assert.Equal("next", next.Value);
    }

    [Fact]
    public void Lexer_BackslashBeforeNewlineIsStillUnterminatedString()
    {
        var lexer = CreateLexer("\"hello\\\r\nnext");

        var token = lexer.Lex();
        var next = lexer.Lex();

        Assert.Equal(TokenKind.BadToken, token.Kind);
        Assert.Equal(DiagnosticCode.UnterminatedString, Assert.Single(lexer.Diagnostics).Code);
        Assert.Equal(TokenKind.Identifier, next.Kind);
        Assert.Equal(new SourcePosition(9, 2, 1), next.Span.Start);
    }

    private static Lexer CreateLexer(string text) => new(new SourceText(text));

    private static string lexerText(SourceSpan span, string source)
    {
        return source[span.Start.Offset..span.End.Offset];
    }
}
