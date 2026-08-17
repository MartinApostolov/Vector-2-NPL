using Vector.Core.Diagnostics;
using Vector.Core.Modules;
using Vector.Core.Modules.Native;
using Vector.Core.Runtime.Native;
using Vector.Plugins;

namespace Vector.TestPlugin.Acceptance;

public sealed class AcceptancePlugin : IVectorPlugin
{
    public string Id => "fixture.acceptance";

    public int ApiVersion => VectorPluginApi.CurrentVersion;

    // Intentionally public but never exported. Acceptance tests verify that plugin loading
    // does not reflect arbitrary C# methods into Vector modules.
    public double Unregistered(double value) => value + 1000d;

    public void Register(IVectorPluginContext context)
    {
        context.RegisterModule(
            new NativeModuleDefinition(
                new ModuleId(new[] { "accept", "math" }),
                module =>
                {
                    module.Export("answer", NativeValueConverter.FromNumber(42));
                    module.Export(
                        "double",
                        new NativeFunction(
                            "double",
                            1,
                            (_, arguments) =>
                            {
                                var value = NativeValueConverter.ToNumber(arguments[0], "value");
                                return NativeValueConverter.FromNumber(value * 2d);
                            }));
                }));

        context.RegisterModule(
            new NativeModuleDefinition(
                new ModuleId(new[] { "accept", "text" }),
                module => module.Export(
                    "greet",
                    new NativeFunction(
                        "greet",
                        1,
                        (_, arguments) =>
                        {
                            var name = NativeValueConverter.ToText(arguments[0], "name");
                            return NativeValueConverter.FromText($"Hello, {name}!");
                        }))));

        context.RegisterModule(
            new NativeModuleDefinition(
                new ModuleId(new[] { "accept", "errors" }),
                module =>
                {
                    module.Export(
                        "explicitFailure",
                        new NativeFunction(
                            "explicitFailure",
                            0,
                            (_, _) => throw new NativeRuntimeException(
                                DiagnosticCode.RuntimeTypeError,
                                "Acceptance plugin deliberate failure.")));
                    module.Export(
                        "unexpectedFailure",
                        new NativeFunction(
                            "unexpectedFailure",
                            0,
                            (_, _) => throw new InvalidOperationException(
                                "acceptance plugin implementation secret")));
                    module.Export(
                        "invalidNull",
                        new NativeFunction(
                            "invalidNull",
                            0,
                            (_, _) => null!));
                }));
    }
}
