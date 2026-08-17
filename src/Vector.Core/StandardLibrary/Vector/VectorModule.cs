using Vector.Core.Diagnostics;
using Vector.Core.Modules;
using Vector.Core.Modules.Native;
using Vector.Core.Runtime.Native;
using Vector.Core.Runtime.Values;
using Vector.Core.StandardLibrary.LinearAlgebra;

namespace Vector.Core.StandardLibrary.Vector;

/// <summary>
/// C#/.NET-backed vector mathematics over ordinary finite numeric Vector lists.
/// </summary>
public static class VectorModule
{
    public static ModuleId Id { get; } = new(new[] { "lib", "vector" });

    public static NativeModuleDefinition CreateDefinition() =>
        new(Id, Initialize);

    public static void Register(NativeModuleRegistry registry)
    {
        ArgumentNullException.ThrowIfNull(registry);
        registry.Register(CreateDefinition());
    }

    private static void Initialize(NativeModuleContext context)
    {
        context.Export("dot", new NativeFunction("dot", 2, (_, arguments) => Dot(arguments[0], arguments[1])));
        context.Export("magnitude", new NativeFunction("magnitude", 1, (_, arguments) => Magnitude(arguments[0])));
        context.Export("normalize", new NativeFunction("normalize", 1, (_, arguments) => Normalize(arguments[0])));
    }

    private static NumberValue Dot(VectorValue leftValue, VectorValue rightValue)
    {
        var left = NumericListReader.Read(leftValue, "a", "lib.vector.dot");
        var right = NumericListReader.Read(rightValue, "b", "lib.vector.dot");

        if (left.Length != right.Length)
        {
            throw new NativeRuntimeException(
                DiagnosticCode.VectorLengthMismatch,
                $"lib.vector.dot requires vectors of equal length, but received lengths {left.Length} and {right.Length}.");
        }

        var total = 0d;
        for (var i = 0; i < left.Length; i++)
        {
            var product = left[i] * right[i];
            EnsureFinite(product, "lib.vector.dot produced a non-finite intermediate product.");

            total += product;
            EnsureFinite(total, "lib.vector.dot produced a non-finite intermediate sum.");
        }

        return NativeValueConverter.FromNumber(total);
    }

    private static NumberValue Magnitude(VectorValue value)
    {
        var numbers = NumericListReader.Read(value, "v", "lib.vector.magnitude");
        return NativeValueConverter.FromNumber(CalculateMagnitude(numbers));
    }

    private static ListValue Normalize(VectorValue value)
    {
        var numbers = NumericListReader.Read(value, "v", "lib.vector.normalize");
        var magnitude = CalculateMagnitude(numbers);

        if (magnitude == 0d)
        {
            throw new NativeRuntimeException(
                DiagnosticCode.NativeRuntimeFailure,
                "lib.vector.normalize cannot normalize a zero-magnitude vector.");
        }

        var normalized = new VectorValue[numbers.Length];
        for (var i = 0; i < numbers.Length; i++)
        {
            normalized[i] = NativeValueConverter.FromNumber(numbers[i] / magnitude);
        }

        return NativeValueConverter.FromList(normalized);
    }

    private static double CalculateMagnitude(IReadOnlyList<double> numbers)
    {
        var sumOfSquares = 0d;
        for (var i = 0; i < numbers.Count; i++)
        {
            var square = numbers[i] * numbers[i];
            EnsureFinite(square, "Vector magnitude produced a non-finite intermediate square.");

            sumOfSquares += square;
            EnsureFinite(sumOfSquares, "Vector magnitude produced a non-finite intermediate sum.");
        }

        var magnitude = System.Math.Sqrt(sumOfSquares);
        EnsureFinite(magnitude, "Vector magnitude produced a non-finite result.");
        return magnitude;
    }

    private static void EnsureFinite(double value, string message)
    {
        if (!double.IsFinite(value))
        {
            throw new NativeRuntimeException(DiagnosticCode.NativeRuntimeFailure, message);
        }
    }
}
