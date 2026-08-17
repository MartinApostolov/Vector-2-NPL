using Vector.Core.Diagnostics;
using Vector.Core.Modules;
using Vector.Core.Modules.Native;
using Vector.Core.Runtime.Native;
using Vector.Core.Runtime.Values;

namespace Vector.Core.StandardLibrary.Collections;

/// <summary>
/// C#/.NET-backed aggregate operations for ordinary Vector lists.
/// </summary>
public static class CollectionsModule
{
    public static ModuleId Id { get; } = new(new[] { "lib", "collections" });

    public static NativeModuleDefinition CreateDefinition() =>
        new(Id, Initialize);

    public static void Register(NativeModuleRegistry registry)
    {
        ArgumentNullException.ThrowIfNull(registry);
        registry.Register(CreateDefinition());
    }

    private static void Initialize(NativeModuleContext context)
    {
        context.Export("sum", new NativeFunction("sum", 1, (_, arguments) => Sum(arguments[0])));
        context.Export("min", new NativeFunction("min", 1, (_, arguments) => Min(arguments[0])));
        context.Export("max", new NativeFunction("max", 1, (_, arguments) => Max(arguments[0])));
    }

    private static NumberValue Sum(VectorValue value)
    {
        var numbers = ReadFiniteNumbers(value);
        var total = 0d;

        foreach (var number in numbers)
        {
            total += number;
        }

        return NativeValueConverter.FromNumber(total);
    }

    private static NumberValue Min(VectorValue value)
    {
        var numbers = ReadFiniteNumbers(value);
        if (numbers.Count == 0)
        {
            throw new NativeRuntimeException(
                DiagnosticCode.NativeRuntimeFailure,
                "lib.collections.min requires a non-empty numeric list.");
        }

        var result = numbers[0];
        for (var i = 1; i < numbers.Count; i++)
        {
            result = System.Math.Min(result, numbers[i]);
        }

        return NativeValueConverter.FromNumber(result);
    }

    private static NumberValue Max(VectorValue value)
    {
        var numbers = ReadFiniteNumbers(value);
        if (numbers.Count == 0)
        {
            throw new NativeRuntimeException(
                DiagnosticCode.NativeRuntimeFailure,
                "lib.collections.max requires a non-empty numeric list.");
        }

        var result = numbers[0];
        for (var i = 1; i < numbers.Count; i++)
        {
            result = System.Math.Max(result, numbers[i]);
        }

        return NativeValueConverter.FromNumber(result);
    }

    private static IReadOnlyList<double> ReadFiniteNumbers(VectorValue value)
    {
        var elements = NativeValueConverter.ToList(value, "values");
        var numbers = new double[elements.Count];

        for (var i = 0; i < elements.Count; i++)
        {
            try
            {
                numbers[i] = NativeValueConverter.ToNumber(elements[i], $"values[{i}]");
            }
            catch (NativeRuntimeException error) when (error.Code == DiagnosticCode.RuntimeTypeError)
            {
                throw new NativeRuntimeException(
                    DiagnosticCode.RuntimeTypeError,
                    $"lib.collections requires a list containing only finite numbers; element {i} is {elements[i].TypeName}.");
            }
        }

        return numbers;
    }
}
