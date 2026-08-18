using Vector.Core.Diagnostics;
using Vector.Core.Parsing;
using Vector.Core.Runtime;
using Vector.Core.Runtime.Values;
using Vector.Core.Source;
using Xunit;
using RuntimeEnvironment = Vector.Core.Runtime.Environment;

namespace Vector.Tests.Runtime;

public sealed class ListRuntimeTests
{
    [Fact]
    public void InterpreterEvaluatesEmptyListLiteral()
    {
        var result = Assert.IsType<ListValue>(Evaluate("[]"));

        Assert.Empty(result.Elements);
        Assert.True(result.IsNumericList);
    }

    [Fact]
    public void InterpreterEvaluatesMixedAndNestedListLiterals()
    {
        var result = Assert.IsType<ListValue>(Evaluate("[1, \"two\", true, nothing, [3, 4]]"));

        Assert.Equal(5, result.Count);
        Assert.Equal(new NumberValue(1), result[0]);
        Assert.Equal(new TextValue("two"), result[1]);
        Assert.Equal(new BooleanValue(true), result[2]);
        Assert.Same(NothingValue.Instance, result[3]);
        Assert.Equal(
            new ListValue(new VectorValue[] { new NumberValue(3), new NumberValue(4) }),
            result[4]);
    }

    [Fact]
    public void ListLiteralElementsEvaluateLeftToRight()
    {
        var environment = new RuntimeEnvironment();
        environment.Declare("x", new NumberValue(1), Span(0, 1));

        var result = Assert.IsType<ListValue>(Evaluate("[(x = x + 1), (x = x * 10)]", environment));

        Assert.Equal(new NumberValue(2), result[0]);
        Assert.Equal(new NumberValue(20), result[1]);
        Assert.Equal(new NumberValue(20), environment.Get("x", Span(0, 1)));
    }

    [Fact]
    public void IndexingIsZeroBased()
    {
        Assert.Equal(new NumberValue(10), Evaluate("[10, 20, 30][0]"));
        Assert.Equal(new NumberValue(20), Evaluate("[10, 20, 30][1]"));
        Assert.Equal(new NumberValue(30), Evaluate("[10, 20, 30][2]"));
    }

    [Fact]
    public void ChainedIndexingWorksForNestedLists()
    {
        Assert.Equal(new NumberValue(4), Evaluate("[[1, 2], [3, 4]][1][1]"));
    }

    [Fact]
    public void IndexTargetAndIndexEvaluateLeftToRight()
    {
        var environment = new RuntimeEnvironment();
        environment.Declare("x", new NumberValue(0), Span(0, 1));
        environment.Declare(
            "lists",
            new ListValue(new VectorValue[]
            {
                new ListValue(new VectorValue[] { new NumberValue(10), new NumberValue(11) }),
                new ListValue(new VectorValue[] { new NumberValue(20), new NumberValue(21) })
            }),
            Span(0, 5));

        var result = Evaluate("lists[(x = 1)][(x = x + 0)]", environment);

        Assert.Equal(new NumberValue(21), result);
        Assert.Equal(new NumberValue(1), environment.Get("x", Span(0, 1)));
    }

    [Fact]
    public void InvalidIndexTargetIsRejectedBeforeIndexExpressionSideEffects()
    {
        var environment = new RuntimeEnvironment();
        environment.Declare("i", new NumberValue(0), Span(0, 1));

        var error = Assert.Throws<RuntimeError>(() => Evaluate("5[(i = 1)]", environment));

        Assert.Equal(DiagnosticCode.RuntimeTypeError, error.Code);
        Assert.Equal(new NumberValue(0), environment.Get("i", Span(0, 1)));
    }

    [Theory]
    [InlineData("[1][\"0\"]")]
    [InlineData("[1][true]")]
    [InlineData("[1][nothing]")]
    public void IndexMustBeNumber(string source)
    {
        var error = Assert.Throws<RuntimeError>(() => Evaluate(source));

        Assert.Equal(DiagnosticCode.RuntimeTypeError, error.Code);
        Assert.Contains("index", error.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("[1][-1]")]
    [InlineData("[1][0.5]")]
    public void IndexMustBeNonNegativeWholeNumber(string source)
    {
        var error = Assert.Throws<RuntimeError>(() => Evaluate(source));

        Assert.Equal(DiagnosticCode.InvalidListIndex, error.Code);
    }

    [Theory]
    [InlineData("[][0]")]
    [InlineData("[1][1]")]
    [InlineData("[1, 2][20]")]
    public void OutOfRangeIndexReportsStructuredRuntimeError(string source)
    {
        var error = Assert.Throws<RuntimeError>(() => Evaluate(source));

        Assert.Equal(DiagnosticCode.ListIndexOutOfRange, error.Code);
        Assert.True(error.Span.Length >= 1);
    }

    [Theory]
    [InlineData("5[0]")]
    [InlineData("\"text\"[0]")]
    public void IndexingRequiresListTarget(string source)
    {
        var error = Assert.Throws<RuntimeError>(() => Evaluate(source));

        Assert.Equal(DiagnosticCode.RuntimeTypeError, error.Code);
        Assert.Contains("list", error.Message);
    }

    [Fact]
    public void IndexedAssignmentMutatesListAndReturnsAssignedValue()
    {
        var environment = new RuntimeEnvironment();
        var list = new ListValue(new VectorValue[] { new NumberValue(10), new NumberValue(20) });
        environment.Declare("items", list, Span(0, 5));

        var result = Evaluate("items[1] = 50", environment);

        Assert.Equal(new NumberValue(50), result);
        Assert.Equal(new NumberValue(10), list[0]);
        Assert.Equal(new NumberValue(50), list[1]);
    }

    [Theory]
    [InlineData("[1][-1] = 0")]
    [InlineData("[1][0.5] = 0")]
    public void IndexedAssignmentUsesIndexValidation(string source)
    {
        var error = Assert.Throws<RuntimeError>(() => Evaluate(source));

        Assert.Equal(DiagnosticCode.InvalidListIndex, error.Code);
    }

    [Theory]
    [InlineData("[1][1] = 0")]
    [InlineData("[][0] = 1")]
    public void IndexedAssignmentChecksBounds(string source)
    {
        var error = Assert.Throws<RuntimeError>(() => Evaluate(source));

        Assert.Equal(DiagnosticCode.ListIndexOutOfRange, error.Code);
    }

    [Fact]
    public void IndexedAssignmentRequiresListTarget()
    {
        var error = Assert.Throws<RuntimeError>(() => Evaluate("5[0] = 1"));

        Assert.Equal(DiagnosticCode.RuntimeTypeError, error.Code);
        Assert.Contains("list", error.Message);
    }

    [Fact]
    public void IndexedAssignmentCanChangeElementRuntimeType()
    {
        var environment = new RuntimeEnvironment();
        var list = new ListValue(new VectorValue[] { new NumberValue(1) });
        environment.Declare("items", list, Span(0, 5));

        Evaluate("items[0] = \"one\"", environment);

        Assert.Equal(new TextValue("one"), list[0]);
        Assert.False(list.IsNumericList);
    }

    [Fact]
    public void IndexedAssignmentEvaluatesRightSideBeforeTargetIndex()
    {
        var environment = new RuntimeEnvironment();
        var list = new ListValue(new VectorValue[] { new NumberValue(10), new NumberValue(20) });
        environment.Declare("items", list, Span(0, 5));
        environment.Declare("i", new NumberValue(0), Span(0, 1));

        Evaluate("items[(i = i + 1)] = (i = 0)", environment);

        Assert.Equal(new NumberValue(10), list[0]);
        Assert.Equal(new NumberValue(0), list[1]);
        Assert.Equal(new NumberValue(1), environment.Get("i", Span(0, 1)));
    }

    [Fact]
    public void IndexedAssignmentRejectsDirectSelfContainment()
    {
        var environment = new RuntimeEnvironment();
        var list = new ListValue(new VectorValue[] { NothingValue.Instance });
        environment.Declare("items", list, Span(0, 5));

        var error = Assert.Throws<RuntimeError>(() => Evaluate("items[0] = items", environment));

        Assert.Equal(DiagnosticCode.CyclicList, error.Code);
        Assert.Same(NothingValue.Instance, list[0]);
    }

    [Fact]
    public void IndexedAssignmentRejectsIndirectSelfContainment()
    {
        var environment = new RuntimeEnvironment();
        var outer = new ListValue(new VectorValue[] { NothingValue.Instance });
        var inner = new ListValue(new VectorValue[] { outer });
        environment.Declare("outer", outer, Span(0, 5));
        environment.Declare("inner", inner, Span(0, 5));

        var error = Assert.Throws<RuntimeError>(() => Evaluate("outer[0] = inner", environment));

        Assert.Equal(DiagnosticCode.CyclicList, error.Code);
        Assert.Same(NothingValue.Instance, outer[0]);
    }

    private static VectorValue Evaluate(string source, RuntimeEnvironment? environment = null)
    {
        var parser = new Parser(new SourceText(source));
        var parseResult = parser.ParseExpression();

        Assert.Empty(parseResult.Diagnostics);
        return new Interpreter(environment).Evaluate(parseResult.Root);
    }

    private static SourceSpan Span(int startOffset, int endOffset) =>
        new(
            new SourcePosition(startOffset, 1, startOffset + 1),
            new SourcePosition(endOffset, 1, endOffset + 1));
}
