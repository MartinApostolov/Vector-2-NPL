using Vector.Core;
using Vector.Core.Diagnostics;
using Vector.Core.Execution;
using Vector.Core.Runtime.Values;
using Vector.Core.StandardLibrary;
using Vector.Core.StandardLibrary.Vector;
using Xunit;

namespace Vector.Tests.StandardLibrary;

public sealed class VectorModuleTests
{
    [Fact]
    public void DefaultStandardLibraryRegistersLibVector()
    {
        var registry = StandardLibraryRegistry.CreateDefault();

        Assert.True(registry.TryGet(VectorModule.Id, out var definition));
        Assert.NotNull(definition);
        Assert.Equal("lib.vector", definition!.QualifiedNamespace);
    }

    [Fact]
    public void DotReturnsDotProduct()
    {
        AssertNumber("lib.vector.dot([1, 2, 3], [4, 5, 6])", 32);
    }

    [Fact]
    public void DotSupportsNegativeAndFractionalValues()
    {
        AssertNumber("lib.vector.dot([-2, 0.5, 3], [4, 2, -1])", -10);
    }

    [Fact]
    public void DotOfEqualEmptyVectorsReturnsZero()
    {
        AssertNumber("lib.vector.dot([], [])", 0);
    }

    [Fact]
    public void DotRejectsMismatchedLengths()
    {
        var result = Execute("lib.vector.dot([1, 2], [3])");

        Assert.False(result.Success);
        var diagnostic = Assert.Single(result.Diagnostics);
        Assert.Equal(DiagnosticCode.VectorLengthMismatch, diagnostic.Code);
        Assert.Contains("length", diagnostic.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("2", diagnostic.Message);
        Assert.Contains("1", diagnostic.Message);
    }

    [Theory]
    [InlineData("lib.vector.dot(1, [2])")]
    [InlineData("lib.vector.dot([1], true)")]
    [InlineData("lib.vector.magnitude(3)")]
    [InlineData("lib.vector.normalize(\"vector\")")]
    public void VectorFunctionsRequireListArguments(string expression)
    {
        var result = Execute(expression);

        Assert.False(result.Success);
        Assert.Equal(DiagnosticCode.RuntimeTypeError, Assert.Single(result.Diagnostics).Code);
    }

    [Theory]
    [InlineData("lib.vector.dot([1, \"two\"], [3, 4])")]
    [InlineData("lib.vector.dot([1, 2], [3, false])")]
    [InlineData("lib.vector.magnitude([1, [2]])")]
    [InlineData("lib.vector.normalize([1, nothing])")]
    public void VectorFunctionsRejectNonNumericElements(string expression)
    {
        var result = Execute(expression);

        Assert.False(result.Success);
        var diagnostic = Assert.Single(result.Diagnostics);
        Assert.Equal(DiagnosticCode.RuntimeTypeError, diagnostic.Code);
        Assert.Contains("numeric lists", diagnostic.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void MagnitudeUsesThreeFourFiveRelationship()
    {
        AssertNumber("lib.vector.magnitude([3, 4])", 5);
    }

    [Fact]
    public void EmptyVectorMagnitudeIsZero()
    {
        AssertNumber("lib.vector.magnitude([])", 0);
    }

    [Fact]
    public void NormalizeReturnsUnitVector()
    {
        var result = Execute("lib.vector.normalize([3, 4])");

        Assert.True(result.Success);
        Assert.Equal(
            new ListValue(new VectorValue[] { new NumberValue(0.6), new NumberValue(0.8) }),
            result.Result);
    }

    [Theory]
    [InlineData("lib.vector.normalize([])")]
    [InlineData("lib.vector.normalize([0, 0])")]
    public void NormalizeRejectsZeroMagnitudeVectors(string expression)
    {
        var result = Execute(expression);

        Assert.False(result.Success);
        var diagnostic = Assert.Single(result.Diagnostics);
        Assert.Equal(DiagnosticCode.NativeRuntimeFailure, diagnostic.Code);
        Assert.Contains("zero-magnitude", diagnostic.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void VectorFunctionsDoNotMutateInputsAndNormalizeAllocatesNewList()
    {
        var result = new VectorEngine().Execute(
            "import lib.vector; " +
            "let a = [3, 4]; let b = [5, 6]; " +
            "let d = lib.vector.dot(a, b); " +
            "let m = lib.vector.magnitude(a); " +
            "let n = lib.vector.normalize(a); " +
            "n[0] = 99; " +
            "[a, b, d, m, n];");

        Assert.True(result.Success);
        Assert.Equal(
            new ListValue(new VectorValue[]
            {
                new ListValue(new VectorValue[] { new NumberValue(3), new NumberValue(4) }),
                new ListValue(new VectorValue[] { new NumberValue(5), new NumberValue(6) }),
                new NumberValue(39),
                new NumberValue(5),
                new ListValue(new VectorValue[] { new NumberValue(99), new NumberValue(0.8) })
            }),
            result.Result);
    }

    [Theory]
    [InlineData("lib.vector.dot([1e308], [2])")]
    [InlineData("lib.vector.magnitude([1e308])")]
    [InlineData("lib.vector.normalize([1e308])")]
    public void NonFiniteIntermediateResultsAreRejected(string expression)
    {
        var result = Execute(expression);

        Assert.False(result.Success);
        Assert.Equal(DiagnosticCode.NativeRuntimeFailure, Assert.Single(result.Diagnostics).Code);
    }

    [Theory]
    [InlineData("lib.vector.dot([1], [2], [3])")]
    [InlineData("lib.vector.magnitude()")]
    [InlineData("lib.vector.normalize([1], [2])")]
    public void VectorFunctionsUseStrictArity(string expression)
    {
        var result = Execute(expression);

        Assert.False(result.Success);
        Assert.Equal(DiagnosticCode.ArgumentCountMismatch, Assert.Single(result.Diagnostics).Code);
    }

    [Fact]
    public void ImportIsRequiredForQualifiedVectorAccess()
    {
        var result = new VectorEngine().Execute("lib.vector.dot([1], [2]);");

        Assert.False(result.Success);
        Assert.Equal(DiagnosticCode.UndefinedVariable, Assert.Single(result.Diagnostics).Code);
    }

    [Theory]
    [InlineData("dot([1], [2]);")]
    [InlineData("magnitude([3, 4]);")]
    [InlineData("normalize([3, 4]);")]
    public void ImportDoesNotLeakUnqualifiedVectorFunctionNames(string source)
    {
        var result = new VectorEngine().Execute($"import lib.vector; {source}");

        Assert.False(result.Success);
        Assert.Equal(DiagnosticCode.UndefinedVariable, Assert.Single(result.Diagnostics).Code);
    }

    [Fact]
    public void ExistingVectorOperatorsContinueToWorkAlongsideLibVector()
    {
        var result = new VectorEngine().Execute(
            "import lib.vector; " +
            "[[1, 2] + [3, 4], [4, 5] - [1, 2], [1, 2] * 3, 3 * [1, 2], " +
            "lib.vector.dot([1, 2], [3, 4])];");

        Assert.True(result.Success);
        Assert.Equal(
            new ListValue(new VectorValue[]
            {
                new ListValue(new VectorValue[] { new NumberValue(4), new NumberValue(6) }),
                new ListValue(new VectorValue[] { new NumberValue(3), new NumberValue(3) }),
                new ListValue(new VectorValue[] { new NumberValue(3), new NumberValue(6) }),
                new ListValue(new VectorValue[] { new NumberValue(3), new NumberValue(6) }),
                new NumberValue(11)
            }),
            result.Result);
    }

    private static ExecutionResult Execute(string expression) =>
        new VectorEngine().Execute($"import lib.vector; {expression};");

    private static void AssertNumber(string expression, double expected)
    {
        var result = Execute(expression);

        Assert.True(result.Success);
        Assert.Equal(expected, Assert.IsType<NumberValue>(result.Result).Value);
    }
}
