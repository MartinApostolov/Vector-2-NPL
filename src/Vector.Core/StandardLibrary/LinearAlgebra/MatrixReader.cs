using Vector.Core.Diagnostics;
using Vector.Core.Runtime.Native;
using Vector.Core.Runtime.Values;

namespace Vector.Core.StandardLibrary.LinearAlgebra;

/// <summary>
/// Internal validated representation used by lib.matrix. Vector matrices remain
/// ordinary nested lists at the language/runtime boundary.
/// </summary>
internal sealed class MatrixData
{
    public MatrixData(double[][] rows)
    {
        ArgumentNullException.ThrowIfNull(rows);
        Rows = rows;
    }

    public double[][] Rows { get; }

    public int RowCount => Rows.Length;

    public int ColumnCount => Rows[0].Length;
}

/// <summary>
/// Validates the standard-library matrix convention: a non-empty rectangular list
/// of non-empty finite numeric rows.
/// </summary>
internal static class MatrixReader
{
    public static MatrixData Read(VectorValue value, string parameterName, string operationName)
    {
        ArgumentNullException.ThrowIfNull(value);
        ArgumentException.ThrowIfNullOrWhiteSpace(parameterName);
        ArgumentException.ThrowIfNullOrWhiteSpace(operationName);

        IReadOnlyList<VectorValue> rowValues;
        try
        {
            rowValues = NativeValueConverter.ToList(value, parameterName);
        }
        catch (NativeRuntimeException error) when (error.Code == DiagnosticCode.RuntimeTypeError)
        {
            throw new NativeRuntimeException(
                DiagnosticCode.RuntimeTypeError,
                $"{operationName} requires a matrix represented by a nested numeric list; " +
                $"{parameterName} is {value.TypeName}.");
        }

        if (rowValues.Count == 0)
        {
            throw InvalidMatrix(operationName, "the matrix must contain at least one row.");
        }

        var rows = new double[rowValues.Count][];
        int? columnCount = null;

        for (var rowIndex = 0; rowIndex < rowValues.Count; rowIndex++)
        {
            var rowValue = rowValues[rowIndex];
            IReadOnlyList<VectorValue> cells;

            try
            {
                cells = NativeValueConverter.ToList(rowValue, $"{parameterName}[{rowIndex}]");
            }
            catch (NativeRuntimeException error) when (error.Code == DiagnosticCode.RuntimeTypeError)
            {
                throw InvalidMatrix(
                    operationName,
                    $"row {rowIndex} must be a list, but is {rowValue.TypeName}.");
            }

            if (cells.Count == 0)
            {
                throw InvalidMatrix(operationName, $"row {rowIndex} must not be empty.");
            }

            if (columnCount is null)
            {
                columnCount = cells.Count;
            }
            else if (cells.Count != columnCount.Value)
            {
                throw InvalidMatrix(
                    operationName,
                    $"the matrix must be rectangular; row 0 has {columnCount.Value} columns " +
                    $"but row {rowIndex} has {cells.Count}.");
            }

            rows[rowIndex] = new double[cells.Count];
            for (var columnIndex = 0; columnIndex < cells.Count; columnIndex++)
            {
                try
                {
                    rows[rowIndex][columnIndex] = NativeValueConverter.ToNumber(
                        cells[columnIndex],
                        $"{parameterName}[{rowIndex}][{columnIndex}]");
                }
                catch (NativeRuntimeException error) when (error.Code == DiagnosticCode.RuntimeTypeError)
                {
                    throw InvalidMatrix(
                        operationName,
                        $"cell [{rowIndex}, {columnIndex}] must be a finite number, " +
                        $"but is {cells[columnIndex].TypeName}.");
                }
            }
        }

        return new MatrixData(rows);
    }

    private static NativeRuntimeException InvalidMatrix(string operationName, string detail) =>
        new(
            DiagnosticCode.RuntimeTypeError,
            $"{operationName} requires a non-empty rectangular matrix with non-empty numeric rows; {detail}");
}
