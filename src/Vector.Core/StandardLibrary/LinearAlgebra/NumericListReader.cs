using Vector.Core.Diagnostics;
using Vector.Core.Runtime.Native;
using Vector.Core.Runtime.Values;

namespace Vector.Core.StandardLibrary.LinearAlgebra;

/// <summary>
/// Internal validation shared by standard-library operations that interpret ordinary
/// Vector lists as finite numeric vectors. This is not a new runtime value type.
/// </summary>
internal static class NumericListReader
{
    public static double[] Read(VectorValue value, string parameterName, string operationName)
    {
        ArgumentNullException.ThrowIfNull(value);
        ArgumentException.ThrowIfNullOrWhiteSpace(parameterName);
        ArgumentException.ThrowIfNullOrWhiteSpace(operationName);

        var elements = NativeValueConverter.ToList(value, parameterName);
        var numbers = new double[elements.Count];

        for (var i = 0; i < elements.Count; i++)
        {
            try
            {
                numbers[i] = NativeValueConverter.ToNumber(elements[i], $"{parameterName}[{i}]");
            }
            catch (NativeRuntimeException error) when (error.Code == DiagnosticCode.RuntimeTypeError)
            {
                throw new NativeRuntimeException(
                    DiagnosticCode.RuntimeTypeError,
                    $"{operationName} requires numeric lists containing only finite numbers; " +
                    $"{parameterName}[{i}] is {elements[i].TypeName}.");
            }
        }

        return numbers;
    }
}
