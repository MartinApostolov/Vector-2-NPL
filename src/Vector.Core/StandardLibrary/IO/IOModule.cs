using Vector.Core.Diagnostics;
using Vector.Core.Modules;
using Vector.Core.Modules.Native;
using Vector.Core.Runtime;
using Vector.Core.Runtime.Host;
using Vector.Core.Runtime.Native;
using Vector.Core.Runtime.Values;

namespace Vector.Core.StandardLibrary.IO;

/// <summary>
/// C#/.NET-backed console/host input functions.
/// </summary>
public static class IOModule
{
    public static ModuleId Id { get; } = new(new[] { "lib", "io" });

    public static NativeModuleDefinition CreateDefinition() =>
        new(Id, Initialize);

    public static void Register(NativeModuleRegistry registry)
    {
        ArgumentNullException.ThrowIfNull(registry);
        registry.Register(CreateDefinition());
    }

    private static void Initialize(NativeModuleContext context)
    {
        context.Export("readLine", new NativeFunction("readLine", 0, (interpreter, _) => ReadLine(interpreter)));
    }

    private static VectorValue ReadLine(Interpreter interpreter)
    {
        if (interpreter.Host is not IVectorInputHost inputHost)
        {
            throw new NativeRuntimeException(
                DiagnosticCode.NativeRuntimeFailure,
                "lib.io.readLine requires an input-capable Vector host.");
        }

        return NativeValueConverter.FromNullableText(inputHost.ReadLine());
    }
}
