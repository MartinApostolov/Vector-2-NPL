using Vector.Core.Lexing;
using Vector.Core.Source;
using Vector.Core.Syntax;
using Vector.Core.Syntax.Expressions;
using Xunit;

namespace Vector.Tests.Syntax;

public sealed class ExpressionSyntaxTests
{
    [Fact]
    public void LiteralExpression_StoresValueAndSpan()
    {
        var span = Span(0, 4);
        var expression = new LiteralExpression(12.5d, span);

        Assert.Equal(12.5d, expression.Value);
        Assert.Equal(span, expression.Span);
    }

    [Fact]
    public void LiteralExpression_AllowsNullForNothingLiteral()
    {
        var expression = new LiteralExpression(null, Span(0, 7));

        Assert.Null(expression.Value);
    }

    [Fact]
    public void NameExpression_StoresNameAndSpan()
    {
        var span = Span(2, 8);
        var expression = new NameExpression("здраве", span);

        Assert.Equal("здраве", expression.Name);
        Assert.Equal(span, expression.Span);
    }

    [Fact]
    public void NameExpression_RejectsNullName()
    {
        Assert.Throws<ArgumentNullException>(() => new NameExpression(null!, Span(0, 0)));
    }

    [Fact]
    public void NameExpression_RejectsEmptyName()
    {
        Assert.Throws<ArgumentException>(() => new NameExpression(string.Empty, Span(0, 0)));
    }

    [Fact]
    public void UnaryExpression_StoresOperatorAndOperand()
    {
        var operand = new LiteralExpression(5d, Span(1, 2));
        var op = Token(TokenKind.Minus, "-", 0, 1);
        var expression = new UnaryExpression(op, operand, Span(0, 2));

        Assert.Same(op, expression.OperatorToken);
        Assert.Same(operand, expression.Operand);
        Assert.Equal(Span(0, 2), expression.Span);
    }

    [Fact]
    public void BinaryExpression_StoresBothOperandsAndOperator()
    {
        var left = new LiteralExpression(2d, Span(0, 1));
        var op = Token(TokenKind.Plus, "+", 2, 3);
        var right = new LiteralExpression(3d, Span(4, 5));
        var expression = new BinaryExpression(left, op, right, Span(0, 5));

        Assert.Same(left, expression.Left);
        Assert.Same(op, expression.OperatorToken);
        Assert.Same(right, expression.Right);
    }

    [Fact]
    public void GroupingExpression_StoresInnerExpressionAndOuterSpan()
    {
        var inner = new NameExpression("value", Span(1, 6));
        var expression = new GroupingExpression(inner, Span(0, 7));

        Assert.Same(inner, expression.Expression);
        Assert.Equal(Span(0, 7), expression.Span);
    }

    [Fact]
    public void AssignmentExpression_StoresTargetEqualsTokenAndValue()
    {
        var target = new NameExpression("x", Span(0, 1));
        var equals = Token(TokenKind.Equals, "=", 2, 3);
        var value = new LiteralExpression(10d, Span(4, 6));
        var expression = new AssignmentExpression(target, equals, value, Span(0, 6));

        Assert.Same(target, expression.Target);
        Assert.Same(equals, expression.EqualsToken);
        Assert.Same(value, expression.Value);
    }

    [Fact]
    public void CallExpression_CopiesArgumentsAndStoresCallee()
    {
        var callee = new NameExpression("print", Span(0, 5));
        var argument = new LiteralExpression("hello", Span(6, 13));
        var sourceArguments = new List<ExpressionSyntax> { argument };
        var expression = new CallExpression(callee, sourceArguments, Span(0, 14));

        sourceArguments.Clear();

        Assert.Same(callee, expression.Callee);
        Assert.Single(expression.Arguments);
        Assert.Same(argument, expression.Arguments[0]);
    }

    [Fact]
    public void CallExpression_RejectsNullCalleeOrArguments()
    {
        var callee = new NameExpression("f", Span(0, 1));

        Assert.Throws<ArgumentNullException>(() =>
            new CallExpression(null!, Array.Empty<ExpressionSyntax>(), Span(0, 0)));
        Assert.Throws<ArgumentNullException>(() =>
            new CallExpression(callee, null!, Span(0, 0)));
    }

    [Fact]
    public void IndexExpression_StoresTargetAndIndex()
    {
        var target = new NameExpression("items", Span(0, 5));
        var index = new LiteralExpression(0d, Span(6, 7));
        var expression = new IndexExpression(target, index, Span(0, 8));

        Assert.Same(target, expression.Target);
        Assert.Same(index, expression.Index);
    }

    [Fact]
    public void ListExpression_CopiesElementsAndRejectsNullCollection()
    {
        var first = new LiteralExpression(1d, Span(1, 2));
        var sourceElements = new List<ExpressionSyntax> { first };
        var expression = new ListExpression(sourceElements, Span(0, 3));

        sourceElements.Clear();

        Assert.Single(expression.Elements);
        Assert.Same(first, expression.Elements[0]);
        Assert.Throws<ArgumentNullException>(() => new ListExpression(null!, Span(0, 0)));
    }

    private static SourceSpan Span(int start, int end) =>
        new(
            new SourcePosition(start, 1, start + 1),
            new SourcePosition(end, 1, end + 1));

    private static Token Token(TokenKind kind, string text, int start, int end) =>
        new(kind, text, null, Span(start, end));
}
