using Vector.Core.Diagnostics;
using Vector.Core.Runtime;
using Vector.Core.Runtime.Values;
using Vector.Core.Source;
using Xunit;

namespace Vector.Tests.Runtime;

public sealed class RuntimeOperationsTests
{
    [Fact]
    public void UnaryAndEqualityOperationsPreserveCoreValueSemantics()
    {
        Assert.Equal(Number(-2), RuntimeOperations.Negate(Number(2), Span(0, 1)));
        Assert.Equal(new BooleanValue(false), RuntimeOperations.LogicalNot(new BooleanValue(true), Span(0, 1)));
        Assert.True(RuntimeOperations.Equal(List(1, 2), List(1, 2)).Value);
        Assert.True(RuntimeOperations.NotEqual(Number(1), Text("1")).Value);
    }

    [Fact]
    public void ArithmeticOperationsPreserveCoreValueSemantics()
    {
        Assert.Equal(new NumberValue(5), RuntimeOperations.Add(Number(2), Number(3), Span(0, 1), Span(4, 5), Span(0, 5)));
        Assert.Equal(new TextValue("Vector VM"), RuntimeOperations.Add(Text("Vector "), Text("VM"), Span(0, 1), Span(4, 5), Span(0, 5)));
        Assert.Equal(new NumberValue(-1), RuntimeOperations.Subtract(Number(2), Number(3), Span(0, 1), Span(4, 5), Span(0, 5)));
        Assert.Equal(new NumberValue(6), RuntimeOperations.Multiply(Number(2), Number(3), Span(0, 1), Span(4, 5), Span(0, 5)));
        Assert.Equal(new NumberValue(2), RuntimeOperations.Divide(Number(6), Number(3), Span(0, 1), Span(4, 5)));
        Assert.Equal(new NumberValue(1), RuntimeOperations.Remainder(Number(7), Number(3), Span(0, 1), Span(4, 5)));
    }

    [Fact]
    public void ComparisonUsesSharedNumberValidation()
    {
        var result = RuntimeOperations.Compare(
            Number(2),
            Number(3),
            Span(0, 1),
            Span(4, 5),
            "<",
            (left, right) => left < right);

        Assert.True(result.Value);
    }

    [Fact]
    public void DivisionAndRemainderByZeroKeepRightOperandSpan()
    {
        var rightSpan = Span(4, 5);

        var division = Assert.Throws<RuntimeError>(() =>
            RuntimeOperations.Divide(Number(6), Number(0), Span(0, 1), rightSpan));
        var remainder = Assert.Throws<RuntimeError>(() =>
            RuntimeOperations.Remainder(Number(6), Number(0), Span(0, 1), rightSpan));

        Assert.Equal(DiagnosticCode.DivisionByZero, division.Code);
        Assert.Equal(rightSpan, division.Span);
        Assert.Equal(DiagnosticCode.DivisionByZero, remainder.Code);
        Assert.Equal(rightSpan, remainder.Span);
    }

    [Fact]
    public void RuntimeTypeValidationKeepsRequestedSpanAndMessageContext()
    {
        var span = Span(10, 14);

        var numberError = Assert.Throws<RuntimeError>(() =>
            RuntimeOperations.RequireNumber(Text("no"), span, "Expected a number"));
        var booleanError = Assert.Throws<RuntimeError>(() =>
            RuntimeOperations.RequireBoolean(Number(1), span, "Expected a boolean"));
        var listError = Assert.Throws<RuntimeError>(() =>
            RuntimeOperations.RequireList(Number(1), span, "Expected a list"));

        Assert.Equal(DiagnosticCode.RuntimeTypeError, numberError.Code);
        Assert.Equal(span, numberError.Span);
        Assert.Contains("Expected a number", numberError.Message);
        Assert.Equal(DiagnosticCode.RuntimeTypeError, booleanError.Code);
        Assert.Equal(span, booleanError.Span);
        Assert.Contains("Expected a boolean", booleanError.Message);
        Assert.Equal(DiagnosticCode.RuntimeTypeError, listError.Code);
        Assert.Equal(span, listError.Span);
        Assert.Contains("Expected a list", listError.Message);
    }

    [Fact]
    public void VectorPairOperationsReturnNewListsAndPreserveOperands()
    {
        var left = List(1, 2);
        var right = List(3, 4);

        var result = Assert.IsType<ListValue>(RuntimeOperations.Add(
            left,
            right,
            Span(0, 1),
            Span(4, 5),
            Span(0, 5)));

        Assert.Equal(List(4, 6), result);
        Assert.NotSame(left, result);
        Assert.NotSame(right, result);
        Assert.Equal(List(1, 2), left);
        Assert.Equal(List(3, 4), right);
    }

    [Fact]
    public void VectorPairOperationsKeepLengthMismatchDiagnostic()
    {
        var operationSpan = Span(0, 10);

        var error = Assert.Throws<RuntimeError>(() => RuntimeOperations.Subtract(
            List(1),
            List(2, 3),
            Span(0, 3),
            Span(6, 10),
            operationSpan));

        Assert.Equal(DiagnosticCode.VectorLengthMismatch, error.Code);
        Assert.Equal(operationSpan, error.Span);
    }

    [Fact]
    public void ScalarMultiplicationValidatesCurrentListContents()
    {
        var list = List(1, 2);
        var result = Assert.IsType<ListValue>(RuntimeOperations.Multiply(
            list,
            Number(2),
            Span(0, 3),
            Span(6, 7),
            Span(0, 7)));

        Assert.Equal(List(2, 4), result);

        list[1] = Text("two");
        var error = Assert.Throws<RuntimeError>(() => RuntimeOperations.Multiply(
            list,
            Number(2),
            Span(0, 3),
            Span(6, 7),
            Span(0, 7)));

        Assert.Equal(DiagnosticCode.RuntimeTypeError, error.Code);
        Assert.Contains("numeric list", error.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(0.5)]
    [InlineData(double.PositiveInfinity)]
    public void ListIndexValidationRejectsInvalidNumbers(double value)
    {
        var error = Assert.Throws<RuntimeError>(() =>
            RuntimeOperations.RequireListIndex(Number(value), Span(3, 4)));

        Assert.Equal(DiagnosticCode.InvalidListIndex, error.Code);
    }

    [Fact]
    public void SharedIndexOperationsReadAndMutateLists()
    {
        var list = List(10, 20, 30);

        var read = RuntimeOperations.GetIndex(list, Number(1), Span(4, 5));
        var written = RuntimeOperations.SetIndex(
            list,
            Number(1),
            Span(4, 5),
            Number(99),
            Span(0, 8));

        Assert.Equal(Number(20), read);
        Assert.Equal(Number(99), written);
        Assert.Equal(Number(99), list[1]);
    }

    [Fact]
    public void SharedIndexOperationsKeepOutOfRangeAndCycleProtection()
    {
        var list = new ListValue(new VectorValue[] { NothingValue.Instance });
        var indexSpan = Span(6, 7);

        var rangeError = Assert.Throws<RuntimeError>(() =>
            RuntimeOperations.GetIndex(list, Number(1), indexSpan));
        Assert.Equal(DiagnosticCode.ListIndexOutOfRange, rangeError.Code);
        Assert.Equal(indexSpan, rangeError.Span);

        var assignmentSpan = Span(0, 15);
        var cycleError = Assert.Throws<RuntimeError>(() =>
            RuntimeOperations.SetIndex(list, Number(0), indexSpan, list, assignmentSpan));
        Assert.Equal(DiagnosticCode.CyclicList, cycleError.Code);
        Assert.Equal(assignmentSpan, cycleError.Span);
        Assert.Same(NothingValue.Instance, list[0]);
    }

    private static NumberValue Number(double value) => new(value);

    private static TextValue Text(string value) => new(value);

    private static ListValue List(params double[] values) =>
        new(values.Select(value => (VectorValue)new NumberValue(value)));

    private static SourceSpan Span(int startOffset, int endOffset) =>
        new(
            new SourcePosition(startOffset, 1, startOffset + 1),
            new SourcePosition(endOffset, 1, endOffset + 1));
}
