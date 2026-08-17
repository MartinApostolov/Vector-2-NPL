using Vector.Core.Modules;
using Vector.Core.Modules.Native;
using Vector.Core.Runtime.Native;
using Vector.Plugins;

namespace Vector.ExamplePlugin;

/// <summary>
/// A small separately compiled Vector plugin intended as a copyable developer example.
/// </summary>
public sealed class ExamplePlugin : IVectorPlugin
{
    public string Id => "example.tools.plugin";

    public int ApiVersion => VectorPluginApi.CurrentVersion;

    public void Register(IVectorPluginContext context)
    {
        context.RegisterModule(
            new NativeModuleDefinition(
                new ModuleId(new[] { "example", "tools" }),
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
                                return NativeValueConverter.FromNumber(value * 2);
                            }));
                    module.Export(
                        "greet",
                        new NativeFunction(
                            "greet",
                            1,
                            (_, arguments) =>
                            {
                                var name = NativeValueConverter.ToText(arguments[0], "name");
                                return NativeValueConverter.FromText($"Hello, {name}!");
                            }));
                }));
    }
}
