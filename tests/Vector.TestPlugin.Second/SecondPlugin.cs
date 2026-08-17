using Vector.Core.Modules;
using Vector.Core.Modules.Native;
using Vector.Core.Runtime.Native;
using Vector.Plugins;

namespace Vector.TestPlugin.Second;

public sealed class SecondPlugin : IVectorPlugin
{
    public string Id => "fixture.second";

    public int ApiVersion => VectorPluginApi.CurrentVersion;

    public void Register(IVectorPluginContext context)
    {
        context.RegisterModule(
            new NativeModuleDefinition(
                new ModuleId(new[] { "fixture", "extra" }),
                module => module.Export(
                    "increment",
                    new NativeFunction(
                        "increment",
                        1,
                        (_, arguments) =>
                        {
                            var value = NativeValueConverter.ToNumber(arguments[0], "value");
                            return NativeValueConverter.FromNumber(value + 1d);
                        }))));
    }
}
