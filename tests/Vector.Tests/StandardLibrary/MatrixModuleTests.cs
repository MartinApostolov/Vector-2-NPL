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

    [Fact]
    public void AddOneByOneMatrix()
    {
        AssertMatrix(
            "lib.matrix.add([[2.5]], [[-1]])",
            new[] { new[] { 1.5d } });
    }

    [Fact]
    public void AddSquareMatrices()
    {
        AssertMatrix(
            "lib.matrix.add([[1, 2], [3, 4]], [[5, 6], [7, 8]])",
            new[] { new[] { 6d, 8d }, new[] { 10d, 12d } });
    }

    [Fact]
    public void AddRectangularMatrices()
    {
        AssertMatrix(
            "lib.matrix.add([[1, 2, 3], [4, 5, 6]], [[6, 5, 4], [3, 2, 1]])",
            new[] { new[] { 7d, 7d, 7d }, new[] { 7d, 7d, 7d } });
    }

    [Fact]
    public void AddSupportsNegativeAndFractionalCells()
    {
        AssertMatrix(
            "lib.matrix.add([[-1.5, 2.25]], [[0.5, -0.75]])",
            new[] { new[] { -1d, 1.5d } });
    }

    [Theory]
    [InlineData("lib.matrix.add([[1, 2], [3, 4]], [[5, 6]])", "2x2", "1x2")]
    [InlineData("lib.matrix.add([[1, 2], [3, 4]], [[5, 6, 7], [8, 9, 10]])", "2x2", "2x3")]
    public void AddRejectsDimensionMismatchAndReportsBothShapes(
        string expression,
        string leftShape,
        string rightShape)
    {
        var result = Execute(expression);

        Assert.False(result.Success);
        var diagnostic = Assert.Single(result.Diagnostics);
        Assert.Equal(DiagnosticCode.NativeRuntimeFailure, diagnostic.Code);
        Assert.Contains(leftShape, diagnostic.Message);
        Assert.Contains(rightShape, diagnostic.Message);
        Assert.Contains("equal shapes", diagnostic.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("lib.matrix.add([1, 2], [[1, 2]])")]
    [InlineData("lib.matrix.add([[1, 2], [3]], [[1, 2], [3, 4]])")]
    [InlineData("lib.matrix.add([[1, 2]], [1, 2])")]
    [InlineData("lib.matrix.add([[1, 2]], [[1, true]])")]
    public void AddRejectsMalformedEitherInput(string expression)
    {
        var result = Execute(expression);

        Assert.False(result.Success);
        Assert.Equal(DiagnosticCode.RuntimeTypeError, Assert.Single(result.Diagnostics).Code);
    }

    [Fact]
    public void AddAllocatesNewRowsAndDoesNotMutateInputs()
    {
        var result = new VectorEngine().Execute(
            "import lib.matrix; " +
            "let a = [[1, 2], [3, 4]]; " +
            "let b = [[5, 6], [7, 8]]; " +
            "let sum = lib.matrix.add(a, b); " +
            "sum[0][0] = 99; " +
            "[a, b, sum];");

        Assert.True(result.Success);
        Assert.Equal(
            new ListValue(new VectorValue[]
            {
                Matrix(new[] { new[] { 1d, 2d }, new[] { 3d, 4d } }),
                Matrix(new[] { new[] { 5d, 6d }, new[] { 7d, 8d } }),
                Matrix(new[] { new[] { 99d, 8d }, new[] { 10d, 12d } })
            }),
            result.Result);
    }

    [Fact]
    public void AddRejectsNonFiniteResult()
    {
        var result = Execute("lib.matrix.add([[1e308]], [[1e308]])");

        Assert.False(result.Success);
        var diagnostic = Assert.Single(result.Diagnostics);
        Assert.Equal(DiagnosticCode.NativeRuntimeFailure, diagnostic.Code);
        Assert.Contains("non-finite", diagnostic.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void MultiplyOneByOneMatrix()
    {
        AssertMatrix(
            "lib.matrix.multiply([[2.5]], [[-4]])",
            new[] { new[] { -10d } });
    }

    [Fact]
    public void MultiplySquareMatrices()
    {
        AssertMatrix(
            "lib.matrix.multiply([[1, 2], [3, 4]], [[5, 6], [7, 8]])",
            new[] { new[] { 19d, 22d }, new[] { 43d, 50d } });
    }

    [Fact]
    public void MultiplyRectangularMatrices()
    {
        AssertMatrix(
            "lib.matrix.multiply([[1, 2, 3], [4, 5, 6]], [[7, 8], [9, 10], [11, 12]])",
            new[] { new[] { 58d, 64d }, new[] { 139d, 154d } });
    }

    [Fact]
    public void MultiplyByIdentityPreservesMatrixValues()
    {
        AssertMatrix(
            "lib.matrix.multiply([[2, -3], [4.5, 1]], [[1, 0], [0, 1]])",
            new[] { new[] { 2d, -3d }, new[] { 4.5d, 1d } });
    }

    [Fact]
    public void MultiplySupportsNegativeAndFractionalCells()
    {
        AssertMatrix(
            "lib.matrix.multiply([[-1.5, 2]], [[0.5], [-3]])",
            new[] { new[] { -6.75d } });
    }

    [Fact]
    public void MultiplyRejectsDimensionMismatchAndReportsBothShapes()
    {
        var result = Execute("lib.matrix.multiply([[1, 2, 3]], [[1, 2], [3, 4]])");

        Assert.False(result.Success);
        var diagnostic = Assert.Single(result.Diagnostics);
        Assert.Equal(DiagnosticCode.NativeRuntimeFailure, diagnostic.Code);
        Assert.Contains("1x3", diagnostic.Message);
        Assert.Contains("2x2", diagnostic.Message);
        Assert.Contains("left columns", diagnostic.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("right rows", diagnostic.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("lib.matrix.multiply([1, 2], [[1], [2]])")]
    [InlineData("lib.matrix.multiply([[1, 2], [3]], [[1], [2]])")]
    [InlineData("lib.matrix.multiply([[1, 2]], [1, 2])")]
    [InlineData("lib.matrix.multiply([[1, 2]], [[1], [true]])")]
    public void MultiplyRejectsMalformedEitherInput(string expression)
    {
        var result = Execute(expression);

        Assert.False(result.Success);
        Assert.Equal(DiagnosticCode.RuntimeTypeError, Assert.Single(result.Diagnostics).Code);
    }

    [Fact]
    public void MultiplyAllocatesNewRowsAndDoesNotMutateInputs()
    {
        var result = new VectorEngine().Execute(
            "import lib.matrix; " +
            "let a = [[1, 2], [3, 4]]; " +
            "let b = [[5, 6], [7, 8]]; " +
            "let product = lib.matrix.multiply(a, b); " +
            "product[0][0] = 99; " +
            "[a, b, product];");

        Assert.True(result.Success);
        Assert.Equal(
            new ListValue(new VectorValue[]
            {
                Matrix(new[] { new[] { 1d, 2d }, new[] { 3d, 4d } }),
                Matrix(new[] { new[] { 5d, 6d }, new[] { 7d, 8d } }),
                Matrix(new[] { new[] { 99d, 22d }, new[] { 43d, 50d } })
            }),
            result.Result);
    }

    [Fact]
    public void MultiplyRejectsNonFiniteResult()
    {
        var result = Execute("lib.matrix.multiply([[1e308]], [[1e308]])");

        Assert.False(result.Success);
        var diagnostic = Assert.Single(result.Diagnostics);
        Assert.Equal(DiagnosticCode.NativeRuntimeFailure, diagnostic.Code);
        Assert.Contains("non-finite", diagnostic.Message, StringComparison.OrdinalIgnoreCase);
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
    [InlineData("lib.matrix.add([[1]])")]
    [InlineData("lib.matrix.add([[1]], [[2]], [[3]])")]
    [InlineData("lib.matrix.multiply([[1]])")]
    [InlineData("lib.matrix.multiply([[1]], [[2]], [[3]])")]
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
    [InlineData("add([[1]], [[2]]);")]
    [InlineData("multiply([[1]], [[2]]);")]
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
