using Vector.Core.Diagnostics;
using Vector.Core.Lexing;
using Vector.Core.Parsing;
using Vector.Core.Source;
using Vector.Core.Syntax.Expressions;
using Xunit;

namespace Vector.Tests.Parsing;

public sealed class ExpressionParserTests
{
    public static TheoryData<string, object?> Literals => new()
    {
        { "12.5", 12.5d },
        { "\"hello\"", "hello" },
        { "true", true },
        { "false", false },
        { "nothing", null }
    };

    [Theory]
    [MemberData(nameof(Literals))]
    public void Parser_ParsesLiteralExpressions(string text, object? expected)
    {
        var result = Parse(text);

        var literal = Assert.IsType<LiteralExpression>(result.Root);
        Assert.Equal(expected, literal.Value);
        Assert.Equal(0, literal.Span.Start.Offset);
        Assert.Equal(text.Length, literal.Span.End.Offset);
        Assert.Empty(result.Diagnostics);
    }

    [Fact]
    public void Parser_ParsesAndNormalizesNameExpressions()
    {
        const string text = "cafe\u0301";
        var result = Parse(text);

        var name = Assert.IsType<NameExpression>(result.Root);
        Assert.Equal(text.Normalize(), name.Name);
        Assert.Equal(text.Length, name.Span.Length);
        Assert.Empty(result.Diagnostics);
    }

    [Fact]
    public void Parser_ParsesGroupingExpressions()
    {
        var result = Parse("(1 + 2)");

        var grouping = Assert.IsType<GroupingExpression>(result.Root);
        Assert.IsType<BinaryExpression>(grouping.Expression);
        Assert.Equal(0, grouping.Span.Start.Offset);
        Assert.Equal(7, grouping.Span.End.Offset);
        Assert.Empty(result.Diagnostics);
    }

    [Theory]
    [InlineData("-value", TokenKind.Minus)]
    [InlineData("not value", TokenKind.NotKeyword)]
    public void Parser_ParsesUnaryExpressions(string text, TokenKind expectedOperator)
    {
        var result = Parse(text);

        var unary = Assert.IsType<UnaryExpression>(result.Root);
        Assert.Equal(expectedOperator, unary.OperatorToken.Kind);
        Assert.IsType<NameExpression>(unary.Operand);
        Assert.Empty(result.Diagnostics);
    }

    [Fact]
    public void Parser_ParsesEmptyListLiteral()
    {
        var result = Parse("[]");

        var list = Assert.IsType<ListExpression>(result.Root);
        Assert.Empty(list.Elements);
        Assert.Equal(2, list.Span.Length);
        Assert.Empty(result.Diagnostics);
    }

    [Fact]
    public void Parser_ParsesListLiteralElements()
    {
        var result = Parse("[1, \"two\", true]");

        var list = Assert.IsType<ListExpression>(result.Root);
        Assert.Collection(
            list.Elements,
            element => Assert.Equal(1d, Assert.IsType<LiteralExpression>(element).Value),
            element => Assert.Equal("two", Assert.IsType<LiteralExpression>(element).Value),
            element => Assert.Equal(true, Assert.IsType<LiteralExpression>(element).Value));
        Assert.Empty(result.Diagnostics);
    }

    [Fact]
    public void Parser_ParsesCallWithoutArguments()
    {
        var result = Parse("run()");

        var call = Assert.IsType<CallExpression>(result.Root);
        Assert.Equal("run", Assert.IsType<NameExpression>(call.Callee).Name);
        Assert.Empty(call.Arguments);
        Assert.Equal(5, call.Span.Length);
        Assert.Empty(result.Diagnostics);
    }

    [Fact]
    public void Parser_ParsesCallArguments()
    {
        var result = Parse("print(1, \"x\", true)");

        var call = Assert.IsType<CallExpression>(result.Root);
        Assert.Equal(3, call.Arguments.Count);
        Assert.All(call.Arguments, argument => Assert.IsType<LiteralExpression>(argument));
        Assert.Empty(result.Diagnostics);
    }

    [Fact]
    public void Parser_ParsesIndexExpression()
    {
        var result = Parse("items[1 + 2]");

        var index = Assert.IsType<IndexExpression>(result.Root);
        Assert.Equal("items", Assert.IsType<NameExpression>(index.Target).Name);
        Assert.IsType<BinaryExpression>(index.Index);
        Assert.Equal(12, index.Span.Length);
        Assert.Empty(result.Diagnostics);
    }

    [Fact]
    public void Parser_ParsesChainedCallsAndIndexesLeftToRight()
    {
        var result = Parse("make()(1)[0]");

        var index = Assert.IsType<IndexExpression>(result.Root);
        var outerCall = Assert.IsType<CallExpression>(index.Target);
        var innerCall = Assert.IsType<CallExpression>(outerCall.Callee);
        Assert.Equal("make", Assert.IsType<NameExpression>(innerCall.Callee).Name);
        Assert.Empty(innerCall.Arguments);
        Assert.Single(outerCall.Arguments);
        Assert.Empty(result.Diagnostics);
    }

    [Fact]
    public void Parser_ParsesNameAssignment()
    {
        var result = Parse("value = 20");

        var assignment = Assert.IsType<AssignmentExpression>(result.Root);
        Assert.Equal("value", Assert.IsType<NameExpression>(assignment.Target).Name);
        Assert.Equal(TokenKind.Equals, assignment.EqualsToken.Kind);
        Assert.Equal(20d, Assert.IsType<LiteralExpression>(assignment.Value).Value);
        Assert.Empty(result.Diagnostics);
    }

    [Fact]
    public void Parser_ParsesIndexAssignment()
    {
        var result = Parse("items[1] = 50");

        var assignment = Assert.IsType<AssignmentExpression>(result.Root);
        Assert.IsType<IndexExpression>(assignment.Target);
        Assert.Equal(50d, Assert.IsType<LiteralExpression>(assignment.Value).Value);
        Assert.Empty(result.Diagnostics);
    }

    [Fact]
    public void Parser_AssignmentAssociatesRightToLeft()
    {
        var result = Parse("a = b = 1");

        var outer = Assert.IsType<AssignmentExpression>(result.Root);
        Assert.Equal("a", Assert.IsType<NameExpression>(outer.Target).Name);
        var inner = Assert.IsType<AssignmentExpression>(outer.Value);
        Assert.Equal("b", Assert.IsType<NameExpression>(inner.Target).Name);
        Assert.Empty(result.Diagnostics);
    }

    [Fact]
    public void Parser_ReportsInvalidAssignmentTarget()
    {
        var result = Parse("(a + b) = 1");

        Assert.IsType<AssignmentExpression>(result.Root);
        var diagnostic = Assert.Single(result.Diagnostics);
        Assert.Equal(DiagnosticCode.InvalidAssignmentTarget, diagnostic.Code);
        Assert.Equal(DiagnosticSeverity.Error, diagnostic.Severity);
    }

    [Fact]
    public void Parser_PreservesLexerDiagnostics()
    {
        var result = Parse("@");

        Assert.True(result.HasErrors);
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code == DiagnosticCode.InvalidCharacter);
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code == DiagnosticCode.ExpectedExpression);
    }

    [Fact]
    public void Parser_ReportsUnexpectedTrailingToken()
    {
        var result = Parse("1 2");

        Assert.Equal(1d, Assert.IsType<LiteralExpression>(result.Root).Value);
        var diagnostic = Assert.Single(result.Diagnostics);
        Assert.Equal(DiagnosticCode.UnexpectedToken, diagnostic.Code);
        Assert.Contains("2", diagnostic.Message);
    }

    [Fact]
    public void Parser_ReportsMissingExpressionAtEndOfFile()
    {
        var result = Parse(string.Empty);

        Assert.True(result.HasErrors);
        var diagnostic = Assert.Single(result.Diagnostics);
        Assert.Equal(DiagnosticCode.ExpectedExpression, diagnostic.Code);
        Assert.Equal(0, result.Root.Span.Length);
    }

    [Fact]
    public void Parser_ReportsMissingClosingParenthesis()
    {
        var result = Parse("(1 + 2");

        Assert.IsType<GroupingExpression>(result.Root);
        var diagnostic = Assert.Single(result.Diagnostics);
        Assert.Equal(DiagnosticCode.UnexpectedToken, diagnostic.Code);
        Assert.Contains("')'", diagnostic.Message);
    }

    [Fact]
    public void Parser_ReportsMissingIndexExpressionWithoutConsumingClosingBracket()
    {
        var result = Parse("items[]");

        Assert.IsType<IndexExpression>(result.Root);
        var diagnostic = Assert.Single(result.Diagnostics);
        Assert.Equal(DiagnosticCode.ExpectedExpression, diagnostic.Code);
    }

    [Fact]
    public void Parser_RejectsNullSource()
    {
        Assert.Throws<ArgumentNullException>(() => new Parser(null!));
    }

    private static ParseResult<Vector.Core.Syntax.ExpressionSyntax> Parse(string text) =>
        new Parser(new SourceText(text)).ParseExpression();
}
