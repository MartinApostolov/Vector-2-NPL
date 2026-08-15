using Vector.Core.Lexing;
using Vector.Core.Parsing;
using Vector.Core.Source;
using Vector.Core.Syntax.Expressions;
using Xunit;

namespace Vector.Tests.Parsing;

public sealed class PrecedenceTests
{
    [Theory]
    [InlineData("1 + 2 * 3", TokenKind.Plus)]
    [InlineData("1 * 2 + 3", TokenKind.Plus)]
    [InlineData("1 < 2 + 3", TokenKind.Less)]
    [InlineData("1 + 2 < 3", TokenKind.Less)]
    [InlineData("1 == 2 < 3", TokenKind.EqualEqual)]
    [InlineData("true and 1 == 1", TokenKind.AndKeyword)]
    [InlineData("true or false and true", TokenKind.OrKeyword)]
    [InlineData("1 + 2 == 3 and true or false", TokenKind.OrKeyword)]
    public void Parser_UsesDocumentedBinaryPrecedence(string text, TokenKind expectedRootOperator)
    {
        var result = Parse(text);

        var binary = Assert.IsType<BinaryExpression>(result.Root);
        Assert.Equal(expectedRootOperator, binary.OperatorToken.Kind);
        Assert.Empty(result.Diagnostics);
    }

    [Fact]
    public void Parser_ParenthesesOverrideNormalPrecedence()
    {
        var result = Parse("(1 + 2) * 3");

        var multiply = Assert.IsType<BinaryExpression>(result.Root);
        Assert.Equal(TokenKind.Star, multiply.OperatorToken.Kind);
        var grouping = Assert.IsType<GroupingExpression>(multiply.Left);
        Assert.Equal(TokenKind.Plus, Assert.IsType<BinaryExpression>(grouping.Expression).OperatorToken.Kind);
        Assert.Empty(result.Diagnostics);
    }

    [Fact]
    public void Parser_UnaryOperatorsBindMoreTightlyThanMultiplication()
    {
        var result = Parse("-1 * 2");

        var multiply = Assert.IsType<BinaryExpression>(result.Root);
        Assert.Equal(TokenKind.Star, multiply.OperatorToken.Kind);
        Assert.Equal(TokenKind.Minus, Assert.IsType<UnaryExpression>(multiply.Left).OperatorToken.Kind);
        Assert.Empty(result.Diagnostics);
    }

    [Fact]
    public void Parser_PostfixCallsAndIndexesBindMoreTightlyThanUnary()
    {
        var result = Parse("-f(1)[0]");

        var unary = Assert.IsType<UnaryExpression>(result.Root);
        var index = Assert.IsType<IndexExpression>(unary.Operand);
        Assert.IsType<CallExpression>(index.Target);
        Assert.Empty(result.Diagnostics);
    }

    [Fact]
    public void Parser_AdditionAndSubtractionAssociateLeftToRight()
    {
        var result = Parse("1 - 2 - 3");

        var outer = Assert.IsType<BinaryExpression>(result.Root);
        Assert.Equal(TokenKind.Minus, outer.OperatorToken.Kind);
        var inner = Assert.IsType<BinaryExpression>(outer.Left);
        Assert.Equal(TokenKind.Minus, inner.OperatorToken.Kind);
        Assert.Empty(result.Diagnostics);
    }

    [Fact]
    public void Parser_MultiplicationDivisionAndRemainderAssociateLeftToRight()
    {
        var result = Parse("8 / 4 % 3");

        var outer = Assert.IsType<BinaryExpression>(result.Root);
        Assert.Equal(TokenKind.Percent, outer.OperatorToken.Kind);
        Assert.Equal(TokenKind.Slash, Assert.IsType<BinaryExpression>(outer.Left).OperatorToken.Kind);
        Assert.Empty(result.Diagnostics);
    }

    [Fact]
    public void Parser_ComparisonOperatorsAssociateLeftToRight()
    {
        var result = Parse("1 < 2 <= 3");

        var outer = Assert.IsType<BinaryExpression>(result.Root);
        Assert.Equal(TokenKind.LessOrEqual, outer.OperatorToken.Kind);
        Assert.Equal(TokenKind.Less, Assert.IsType<BinaryExpression>(outer.Left).OperatorToken.Kind);
        Assert.Empty(result.Diagnostics);
    }

    [Fact]
    public void Parser_EqualityOperatorsAssociateLeftToRight()
    {
        var result = Parse("1 == 1 != false");

        var outer = Assert.IsType<BinaryExpression>(result.Root);
        Assert.Equal(TokenKind.BangEqual, outer.OperatorToken.Kind);
        Assert.Equal(TokenKind.EqualEqual, Assert.IsType<BinaryExpression>(outer.Left).OperatorToken.Kind);
        Assert.Empty(result.Diagnostics);
    }

    [Fact]
    public void Parser_AssignmentHasLowerPrecedenceThanOr()
    {
        var result = Parse("x = true or false");

        var assignment = Assert.IsType<AssignmentExpression>(result.Root);
        var value = Assert.IsType<BinaryExpression>(assignment.Value);
        Assert.Equal(TokenKind.OrKeyword, value.OperatorToken.Kind);
        Assert.Empty(result.Diagnostics);
    }

    [Fact]
    public void Parser_CallArgumentsParseCompleteAssignmentExpressions()
    {
        var result = Parse("f(x = 1)");

        var call = Assert.IsType<CallExpression>(result.Root);
        Assert.IsType<AssignmentExpression>(Assert.Single(call.Arguments));
        Assert.Empty(result.Diagnostics);
    }

    private static ParseResult<Vector.Core.Syntax.ExpressionSyntax> Parse(string text) =>
        new Parser(new SourceText(text)).ParseExpression();
}
