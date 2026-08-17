using Vector.Core;
using Vector.Core.Diagnostics;
using Vector.Core.Execution;
using Vector.Core.Runtime.Values;
using Vector.Core.StandardLibrary;
using Vector.Core.StandardLibrary.Matrix;
using Xunit;

namespace Vector.Tests.StandardLibrary;

public sealed class MatrixModuleTests
{
    [Fact]
    public void DefaultStandardLibraryRegistersLibMatrix()
    {
        var registry = StandardLibraryRegistry.CreateDefault();

        Assert.True(registry.TryGet(MatrixModule.Id, out var definition));
        Assert.NotNull(definition);
        Assert.Equal("lib.matrix", definition!.QualifiedNamespace);
    }

    [Theory]
    [InlineData("[[1, 2], [3, 4]]", 2, 2)]
    [InlineData("[[1, 2, 3], [4, 5, 6]]", 2, 3)]
    [InlineData("[[1], [2], [3]]", 3, 1)]
    public void ShapeReturnsRowsAndColumns(string matrix, double rows, double columns)
    {
        var result = Execute($"lib.matrix.shape({matrix})");

        Assert.True(result.Success);
        Assert.Equal(
            new ListValue(new VectorValue[] { new NumberValue(rows), new NumberValue(columns) }),
            result.Result);
    }

    [Fact]
    public void TransposeSquareMatrix()
    {
        AssertMatrix(
            "lib.matrix.transpose([[1, 2], [3, 4]])",
            new[] { new[] { 1d, 3d }, new[] { 2d, 4d } });
    }

    [Fact]
    public void TransposeRectangularMatrix()
    {
        AssertMatrix(
            "lib.matrix.transpose([[1, 2, 3], [4, 5, 6]])",
            new[] { new[] { 1d, 4d }, new[] { 2d, 5d }, new[] { 3d, 6d } });
    }

    [Fact]
    public void TransposeColumnMatrix()
    {
        AssertMatrix(
            "lib.matrix.transpose([[1], [2], [3]])",
            new[] { new[] { 1d, 2d, 3d } });
    }

    [Theory]
    [InlineData("lib.matrix.shape(1)")]
    [InlineData("lib.matrix.transpose(\"matrix\")")]
    public void MatrixFunctionsRequireTopLevelList(string expression)
    {
        var result = Execute(expression);

        Assert.False(result.Success);
        Assert.Equal(DiagnosticCode.RuntimeTypeError, Assert.Single(result.Diagnostics).Code);
    }

    [Theory]
    [InlineData("lib.matrix.shape([])")]
    [InlineData("lib.matrix.transpose([])")]
    [InlineData("lib.matrix.shape([1, 2])")]
    [InlineData("lib.matrix.transpose([[1, 2], 3])")]
    [InlineData("lib.matrix.shape([[], []])")]
    [InlineData("lib.matrix.transpose([[1, 2], []])")]
    [InlineData("lib.matrix.shape([[1, 2], [3]])")]
    [InlineData("lib.matrix.transpose([[1], [2, 3]])")]
    [InlineData("lib.matrix.shape([[1, \"x\"], [2, 3]])")]
    [InlineData("lib.matrix.transpose([[1, true], [2, 3]])")]
    public void MalformedMatricesProduceStructuredTypeErrors(string expression)
    {
        var result = Execute(expression);

        Assert.False(result.Success);
        var diagnostic = Assert.Single(result.Diagnostics);
        Assert.Equal(DiagnosticCode.RuntimeTypeError, diagnostic.Code);
        Assert.Contains("matrix", diagnostic.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void TransposeDoesNotMutateOrAliasInputRows()
    {
        var result = new VectorEngine().Execute(
            "import lib.matrix; " +
            "let matrix = [[1, 2, 3], [4, 5, 6]]; " +
            "let transposed = lib.matrix.transpose(matrix); " +
            "transposed[0][0] = 99; " +
            "[matrix, transposed];");

        Assert.True(result.Success);
        Assert.Equal(
            new ListValue(new VectorValue[]
            {
                Matrix(new[] { new[] { 1d, 2d, 3d }, new[] { 4d, 5d, 6d } }),
                Matrix(new[] { new[] { 99d, 4d }, new[] { 2d, 5d }, new[] { 3d, 6d } })
            }),
            result.Result);
    }

    [Theory]
    [InlineData("lib.matrix.shape()")]
    [InlineData("lib.matrix.shape([[1]], [[2]])")]
    [InlineData("lib.matrix.transpose()")]
    [InlineData("lib.matrix.transpose([[1]], [[2]])")]
    public void MatrixFunctionsUseStrictArity(string expression)
    {
        var result = Execute(expression);

        Assert.False(result.Success);
        Assert.Equal(DiagnosticCode.ArgumentCountMismatch, Assert.Single(result.Diagnostics).Code);
    }

    [Fact]
    public void ImportIsRequiredForQualifiedMatrixAccess()
    {
        var result = new VectorEngine().Execute("lib.matrix.shape([[1]]);");

        Assert.False(result.Success);
        Assert.Equal(DiagnosticCode.UndefinedVariable, Assert.Single(result.Diagnostics).Code);
    }

    [Theory]
    [InlineData("shape([[1]]);")]
    [InlineData("transpose([[1]]);")]
    public void ImportDoesNotLeakUnqualifiedMatrixFunctionNames(string source)
    {
        var result = new VectorEngine().Execute($"import lib.matrix; {source}");

        Assert.False(result.Success);
        Assert.Equal(DiagnosticCode.UndefinedVariable, Assert.Single(result.Diagnostics).Code);
    }

    private static ExecutionResult Execute(string expression) =>
        new VectorEngine().Execute($"import lib.matrix; {expression};");

    private static void AssertMatrix(string expression, double[][] expected)
    {
        var result = Execute(expression);

        Assert.True(result.Success);
        Assert.Equal(Matrix(expected), result.Result);
    }

    private static ListValue Matrix(double[][] rows) =>
        new(rows.Select(row =>
            (VectorValue)new ListValue(row.Select(value => (VectorValue)new NumberValue(value)))));
}
