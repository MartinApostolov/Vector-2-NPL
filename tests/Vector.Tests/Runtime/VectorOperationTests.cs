using Vector.Core.Diagnostics;
using Vector.Core.Parsing;
using Vector.Core.Runtime;
using Vector.Core.Runtime.Values;
using Vector.Core.Source;
using Xunit;
using RuntimeEnvironment = Vector.Core.Runtime.Environment;

namespace Vector.Tests.Runtime;

public sealed class VectorOperationTests
{
    [Theory]
    [InlineData("[1, 2] + [3, 4]", 4d, 6d)]
    [InlineData("[1, 2] - [3, 4]", -2d, -2d)]
    public void NumericListsSupportElementWiseAdditionAndSubtraction(
        string source,
        double first,
        double second)
    {
        var result = Assert.IsType<ListValue>(Evaluate(source));

        Assert.Equal(new NumberValue(first), result[0]);
        Assert.Equal(new NumberValue(second), result[1]);
    }

    [Fact]
    public void VectorOperationsReturnNewListsWithoutMutatingOperands()
    {
        var environment = new RuntimeEnvironment();
        var left = new ListValue(new VectorValue[] { new NumberValue(1), new NumberValue(2) });
        var right = new ListValue(new VectorValue[] { new NumberValue(3), new NumberValue(4) });
        environment.Declare("left", left, Span(0, 4));
        environment.Declare("right", right, Span(0, 5));

        var result = Assert.IsType<ListValue>(Evaluate("left + right", environment));

        Assert.NotSame(left, result);
        Assert.NotSame(right, result);
        Assert.Equal(new ListValue(new VectorValue[] { new NumberValue(1), new NumberValue(2) }), left);
        Assert.Equal(new ListValue(new VectorValue[] { new NumberValue(3), new NumberValue(4) }), right);
    }

    [Theory]
    [InlineData("[1, 2, 3] * 2", 2d, 4d, 6d)]
    [InlineData("2 * [1, 2, 3]", 2d, 4d, 6d)]
    [InlineData("[1.5, -2, 0] * 3", 4.5d, -6d, 0d)]
    public void ScalarMultiplicationWorksInBothDirections(
        string source,
        double first,
        double second,
        double third)
    {
        var result = Assert.IsType<ListValue>(Evaluate(source));

        Assert.Equal(new NumberValue(first), result[0]);
        Assert.Equal(new NumberValue(second), result[1]);
        Assert.Equal(new NumberValue(third), result[2]);
    }

    [Theory]
    [InlineData("[] + []")]
    [InlineData("[] - []")]
    [InlineData("[] * 5")]
    [InlineData("5 * []")]
    public void EmptyListParticipatesAsZeroLengthNumericList(string source)
    {
        var result = Assert.IsType<ListValue>(Evaluate(source));

        Assert.Empty(result.Elements);
        Assert.True(result.IsNumericList);
    }

    [Theory]
    [InlineData("[1] + [2, 3]")]
    [InlineData("[1, 2] - [3]")]
    public void PairwiseVectorOperationsRequireEqualLengths(string source)
    {
        var error = Assert.Throws<RuntimeError>(() => Evaluate(source));

        Assert.Equal(DiagnosticCode.VectorLengthMismatch, error.Code);
    }

    [Theory]
    [InlineData("[1, \"two\"] + [3, 4]")]
    [InlineData("[1, 2] + [3, false]")]
    [InlineData("[1, nothing] - [3, 4]")]
    [InlineData("[1, \"two\"] * 2")]
    [InlineData("2 * [true, 3]")]
    public void VectorOperationsRejectListsThatAreNotCurrentlyNumeric(string source)
    {
        var error = Assert.Throws<RuntimeError>(() => Evaluate(source));

        Assert.Equal(DiagnosticCode.RuntimeTypeError, error.Code);
        Assert.Contains("numeric list", error.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void VectorEligibilityUsesCurrentListContents()
    {
        var environment = new RuntimeEnvironment();
        var list = new ListValue(new VectorValue[] { new NumberValue(1), new NumberValue(2) });
        environment.Declare("values", list, Span(0, 6));

        Assert.Equal(
            new ListValue(new VectorValue[] { new NumberValue(2), new NumberValue(4) }),
            Evaluate("values * 2", environment));

        Evaluate("values[1] = \"two\"", environment);
        var error = Assert.Throws<RuntimeError>(() => Evaluate("values * 2", environment));
        Assert.Equal(DiagnosticCode.RuntimeTypeError, error.Code);

        Evaluate("values[1] = 2", environment);
        Assert.Equal(
            new ListValue(new VectorValue[] { new NumberValue(2), new NumberValue(4) }),
            Evaluate("values * 2", environment));
    }

    [Theory]
    [InlineData("[1, 2] + [3, 4]", true)]
    [InlineData("[1, 2] == [1, 2]", true)]
    [InlineData("[1, 2] != [1, 3]", true)]
    public void ListExpressionsIntegrateWithExistingEqualityAndOperators(string source, bool expectedWhenBoolean)
    {
        var result = Evaluate(source);

        if (result is BooleanValue boolean)
        {
            Assert.Equal(expectedWhenBoolean, boolean.Value);
        }
        else
        {
            Assert.IsType<ListValue>(result);
        }
    }

    [Theory]
    [InlineData("[1, 2] + [3, 4]")]
    [InlineData("[1, 2] - [3, 4]")]
    [InlineData("[1, 2] * 3")]
    public void VectorResultsAreNumericLists(string source)
    {
        var result = Assert.IsType<ListValue>(Evaluate(source));

        Assert.True(result.IsNumericList);
    }

    [Theory]
    [InlineData("[1, 2] * [3, 4]")]
    [InlineData("[1, 2] / 2")]
    [InlineData("[1, 2] % 2")]
    public void UnsupportedListArithmeticRemainsATypeError(string source)
    {
        var error = Assert.Throws<RuntimeError>(() => Evaluate(source));

        Assert.Equal(DiagnosticCode.RuntimeTypeError, error.Code);
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
