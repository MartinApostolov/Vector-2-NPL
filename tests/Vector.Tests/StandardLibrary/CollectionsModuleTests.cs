using Vector.Core;
using Vector.Core.Diagnostics;
using Vector.Core.Execution;
using Vector.Core.Runtime.Values;
using Vector.Core.StandardLibrary;
using Vector.Core.StandardLibrary.Collections;
using Xunit;

namespace Vector.Tests.StandardLibrary;

public sealed class CollectionsModuleTests
{
    [Fact]
    public void DefaultStandardLibraryRegistersLibCollections()
    {
        var registry = StandardLibraryRegistry.CreateDefault();

        Assert.True(registry.TryGet(CollectionsModule.Id, out var definition));
        Assert.NotNull(definition);
        Assert.Equal("lib.collections", definition!.QualifiedNamespace);
    }

    [Fact]
    public void SumReturnsArithmeticTotal()
    {
        AssertNumber("lib.collections.sum([4, -2, 8, 3])", 13);
    }

    [Fact]
    public void SumSupportsNegativeAndFractionalValues()
    {
        AssertNumber("lib.collections.sum([-2.5, 1.25, 4])", 2.75);
    }

    [Fact]
    public void SumOfEmptyListReturnsZero()
    {
        AssertNumber("lib.collections.sum([])", 0);
    }

    [Fact]
    public void MinReturnsSmallestValue()
    {
        AssertNumber("lib.collections.min([4, -2, 8, 3])", -2);
    }

    [Fact]
    public void MaxReturnsLargestValue()
    {
        AssertNumber("lib.collections.max([4, -2, 8, 3])", 8);
    }

    [Fact]
    public void OneElementMinAndMaxReturnThatValue()
    {
        var result = Execute("[lib.collections.min([2.5]), lib.collections.max([2.5])]");

        Assert.True(result.Success);
        Assert.Equal(
            new ListValue(new VectorValue[] { new NumberValue(2.5), new NumberValue(2.5) }),
            result.Result);
    }

    [Theory]
    [InlineData("lib.collections.min([])", "min")]
    [InlineData("lib.collections.max([])", "max")]
    public void MinAndMaxRejectEmptyLists(string expression, string member)
    {
        var result = Execute(expression);

        Assert.False(result.Success);
        var diagnostic = Assert.Single(result.Diagnostics);
        Assert.Equal(DiagnosticCode.NativeRuntimeFailure, diagnostic.Code);
        Assert.Contains($"lib.collections.{member}", diagnostic.Message);
        Assert.Contains("non-empty", diagnostic.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("lib.collections.sum(1)")]
    [InlineData("lib.collections.min(\"values\")")]
    [InlineData("lib.collections.max(true)")]
    public void AggregateFunctionsRequireAList(string expression)
    {
        var result = Execute(expression);

        Assert.False(result.Success);
        Assert.Equal(DiagnosticCode.RuntimeTypeError, Assert.Single(result.Diagnostics).Code);
    }

    [Theory]
    [InlineData("lib.collections.sum([1, \"two\", 3])")]
    [InlineData("lib.collections.min([1, true, 3])")]
    [InlineData("lib.collections.max([1, [2], 3])")]
    public void AggregateFunctionsRejectNonNumericElements(string expression)
    {
        var result = Execute(expression);

        Assert.False(result.Success);
        var diagnostic = Assert.Single(result.Diagnostics);
        Assert.Equal(DiagnosticCode.RuntimeTypeError, diagnostic.Code);
        Assert.Contains("only finite numbers", diagnostic.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("lib.collections.sum([] , [])")]
    [InlineData("lib.collections.min()")]
    [InlineData("lib.collections.max([1], [2])")]
    public void AggregateFunctionsUseStrictArity(string expression)
    {
        var result = Execute(expression);

        Assert.False(result.Success);
        Assert.Equal(DiagnosticCode.ArgumentCountMismatch, Assert.Single(result.Diagnostics).Code);
    }

    [Fact]
    public void AggregateFunctionsDoNotMutateInputList()
    {
        var result = new VectorEngine().Execute(
            "import lib.collections; " +
            "let values = [4, -2, 8, 3]; " +
            "let total = lib.collections.sum(values); " +
            "let smallest = lib.collections.min(values); " +
            "let largest = lib.collections.max(values); " +
            "[values, total, smallest, largest];");

        Assert.True(result.Success);
        Assert.Equal(
            new ListValue(new VectorValue[]
            {
                new ListValue(new VectorValue[]
                {
                    new NumberValue(4), new NumberValue(-2), new NumberValue(8), new NumberValue(3)
                }),
                new NumberValue(13),
                new NumberValue(-2),
                new NumberValue(8)
            }),
            result.Result);
    }

    [Fact]
    public void ImportIsRequiredForQualifiedCollectionsAccess()
    {
        var result = new VectorEngine().Execute("lib.collections.sum([1, 2, 3]);");

        Assert.False(result.Success);
        Assert.Equal(DiagnosticCode.UndefinedVariable, Assert.Single(result.Diagnostics).Code);
    }

    [Theory]
    [InlineData("sum([1, 2, 3]);")]
    [InlineData("min([1, 2, 3]);")]
    [InlineData("max([1, 2, 3]);")]
    public void ImportDoesNotLeakUnqualifiedAggregateNames(string source)
    {
        var result = new VectorEngine().Execute($"import lib.collections; {source}");

        Assert.False(result.Success);
        Assert.Equal(DiagnosticCode.UndefinedVariable, Assert.Single(result.Diagnostics).Code);
    }

    [Fact]
    public void CollectionsMinAndMaxCoexistWithScalarMathMinAndMax()
    {
        var result = new VectorEngine().Execute(
            "import lib.collections; import lib.math; " +
            "[lib.collections.min([7, 3, 9]), lib.collections.max([7, 3, 9]), " +
            "lib.math.min(7, 3), lib.math.max(7, 3)];");

        Assert.True(result.Success);
        Assert.Equal(
            new ListValue(new VectorValue[]
            {
                new NumberValue(3), new NumberValue(9), new NumberValue(3), new NumberValue(7)
            }),
            result.Result);
    }

    [Fact]
    public void OverflowingSumIsRejectedAtNativeBoundary()
    {
        var result = Execute("lib.collections.sum([1e308, 1e308])");

        Assert.False(result.Success);
        Assert.Equal(DiagnosticCode.NativeRuntimeFailure, Assert.Single(result.Diagnostics).Code);
    }

    private static ExecutionResult Execute(string expression) =>
        new VectorEngine().Execute($"import lib.collections; {expression};");

    private static void AssertNumber(string expression, double expected)
    {
        var result = Execute(expression);

        Assert.True(result.Success);
        Assert.Equal(expected, Assert.IsType<NumberValue>(result.Result).Value);
    }
}
