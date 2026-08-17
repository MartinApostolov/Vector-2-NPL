using Vector.Core.Diagnostics;
using Vector.Core.Modules;
using Vector.Core.Modules.Native;
using Vector.Core.Runtime.Native;
using Vector.Core.Runtime.Values;
using Vector.Core.StandardLibrary.LinearAlgebra;

namespace Vector.Core.StandardLibrary.Matrix;

/// <summary>
/// C#/.NET-backed matrix operations over ordinary rectangular nested numeric lists.
/// </summary>
public static class MatrixModule
{
    public static ModuleId Id { get; } = new(new[] { "lib", "matrix" });

    public static NativeModuleDefinition CreateDefinition() =>
        new(Id, Initialize);

    public static void Register(NativeModuleRegistry registry)
    {
        ArgumentNullException.ThrowIfNull(registry);
        registry.Register(CreateDefinition());
    }

    private static void Initialize(NativeModuleContext context)
    {
        context.Export("shape", new NativeFunction("shape", 1, (_, arguments) => Shape(arguments[0])));
        context.Export("transpose", new NativeFunction("transpose", 1, (_, arguments) => Transpose(arguments[0])));
        context.Export("add", new NativeFunction("add", 2, (_, arguments) => Add(arguments[0], arguments[1])));
        context.Export("multiply", new NativeFunction("multiply", 2, (_, arguments) => Multiply(arguments[0], arguments[1])));
    }

    private static ListValue Shape(VectorValue value)
    {
        var matrix = MatrixReader.Read(value, "matrix", "lib.matrix.shape");
        return NativeValueConverter.FromList(new VectorValue[]
        {
            NativeValueConverter.FromNumber(matrix.RowCount),
            NativeValueConverter.FromNumber(matrix.ColumnCount)
        });
    }

    private static ListValue Transpose(VectorValue value)
    {
        var matrix = MatrixReader.Read(value, "matrix", "lib.matrix.transpose");
        var rows = new VectorValue[matrix.ColumnCount];

        for (var columnIndex = 0; columnIndex < matrix.ColumnCount; columnIndex++)
        {
            var transposedRow = new VectorValue[matrix.RowCount];
            for (var rowIndex = 0; rowIndex < matrix.RowCount; rowIndex++)
            {
                transposedRow[rowIndex] = NativeValueConverter.FromNumber(matrix.Rows[rowIndex][columnIndex]);
            }

            rows[columnIndex] = NativeValueConverter.FromList(transposedRow);
        }

        return NativeValueConverter.FromList(rows);
    }

    private static ListValue Add(VectorValue leftValue, VectorValue rightValue)
    {
        var left = MatrixReader.Read(leftValue, "a", "lib.matrix.add");
        var right = MatrixReader.Read(rightValue, "b", "lib.matrix.add");

        if (left.RowCount != right.RowCount || left.ColumnCount != right.ColumnCount)
        {
            throw new NativeRuntimeException(
                DiagnosticCode.NativeRuntimeFailure,
                $"lib.matrix.add requires equal shapes, but received " +
                $"{left.RowCount}x{left.ColumnCount} and {right.RowCount}x{right.ColumnCount}.");
        }

        var rows = new VectorValue[left.RowCount];
        for (var rowIndex = 0; rowIndex < left.RowCount; rowIndex++)
        {
            var row = new VectorValue[left.ColumnCount];
            for (var columnIndex = 0; columnIndex < left.ColumnCount; columnIndex++)
            {
                var sum = left.Rows[rowIndex][columnIndex] + right.Rows[rowIndex][columnIndex];
                if (!double.IsFinite(sum))
                {
                    throw new NativeRuntimeException(
                        DiagnosticCode.NativeRuntimeFailure,
                        $"lib.matrix.add produced a non-finite result at cell [{rowIndex}, {columnIndex}].");
                }

                row[columnIndex] = NativeValueConverter.FromNumber(sum);
            }

            rows[rowIndex] = NativeValueConverter.FromList(row);
        }

        return NativeValueConverter.FromList(rows);
    }

    private static ListValue Multiply(VectorValue leftValue, VectorValue rightValue)
    {
        var left = MatrixReader.Read(leftValue, "a", "lib.matrix.multiply");
        var right = MatrixReader.Read(rightValue, "b", "lib.matrix.multiply");

        if (left.ColumnCount != right.RowCount)
        {
            throw new NativeRuntimeException(
                DiagnosticCode.NativeRuntimeFailure,
                $"lib.matrix.multiply requires left columns to equal right rows, but received " +
                $"{left.RowCount}x{left.ColumnCount} and {right.RowCount}x{right.ColumnCount}.");
        }

        var rows = new VectorValue[left.RowCount];
        for (var rowIndex = 0; rowIndex < left.RowCount; rowIndex++)
        {
            var row = new VectorValue[right.ColumnCount];
            for (var columnIndex = 0; columnIndex < right.ColumnCount; columnIndex++)
            {
                var total = 0d;
                for (var sharedIndex = 0; sharedIndex < left.ColumnCount; sharedIndex++)
                {
                    var product = left.Rows[rowIndex][sharedIndex] * right.Rows[sharedIndex][columnIndex];
                    if (!double.IsFinite(product))
                    {
                        throw new NativeRuntimeException(
                            DiagnosticCode.NativeRuntimeFailure,
                            $"lib.matrix.multiply produced a non-finite result at cell [{rowIndex}, {columnIndex}].");
                    }

                    total += product;
                    if (!double.IsFinite(total))
                    {
                        throw new NativeRuntimeException(
                            DiagnosticCode.NativeRuntimeFailure,
                            $"lib.matrix.multiply produced a non-finite result at cell [{rowIndex}, {columnIndex}].");
                    }
                }

                row[columnIndex] = NativeValueConverter.FromNumber(total);
            }

            rows[rowIndex] = NativeValueConverter.FromList(row);
        }

        return NativeValueConverter.FromList(rows);
    }

}
