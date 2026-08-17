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
}
