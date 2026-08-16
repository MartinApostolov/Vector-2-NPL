using Vector.Core.Diagnostics;
using Vector.Core.Modules;
using Vector.Core.Modules.Native;
using Vector.Core.Runtime.Native;
using Vector.Core.Runtime.Values;

namespace Vector.Core.StandardLibrary.Math;

/// <summary>
/// C#/.NET-backed implementation of Vector's <c>lib.math</c> standard module.
/// </summary>
public static class MathModule
{
    public static ModuleId Id { get; } = new(new[] { "lib", "math" });

    public static NativeModuleDefinition CreateDefinition() =>
        new(Id, Initialize);

    public static void Register(NativeModuleRegistry registry)
    {
        ArgumentNullException.ThrowIfNull(registry);
        registry.Register(CreateDefinition());
    }

    private static void Initialize(NativeModuleContext context)
    {
        context.Export("pi", NativeValueConverter.FromNumber(System.Math.PI));
        context.Export("e", NativeValueConverter.FromNumber(System.Math.E));

        context.Export("abs", Unary("abs", System.Math.Abs));
        context.Export("sqrt", Unary("sqrt", System.Math.Sqrt));
        context.Export("min", Binary("min", System.Math.Min));
        context.Export("max", Binary("max", System.Math.Max));
        context.Export("pow", Binary("pow", System.Math.Pow));
    }

    private static NativeFunction Unary(string name, Func<double, double> implementation) =>
        new(
            name,
            1,
            (_, arguments) =>
            {
                var value = NativeValueConverter.ToNumber(arguments[0], "value");
                return FromMathResult(name, implementation(value));
            });

    private static NativeFunction Binary(string name, Func<double, double, double> implementation) =>
        new(
            name,
            2,
            (_, arguments) =>
            {
                var first = NativeValueConverter.ToNumber(arguments[0], "first");
                var second = NativeValueConverter.ToNumber(arguments[1], "second");
                return FromMathResult(name, implementation(first, second));
            });

    private static NumberValue FromMathResult(string memberName, double result)
    {
        if (!double.IsFinite(result))
        {
            throw new NativeRuntimeException(
                DiagnosticCode.NativeRuntimeFailure,
                $"lib.math.{memberName} produced a non-finite result for the supplied arguments.");
        }

        return NativeValueConverter.FromNumber(result);
    }
}
