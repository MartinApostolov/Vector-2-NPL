using Vector.Core.Modules;
using Vector.Core.Modules.Native;
using Vector.Plugins;

namespace Vector.TestPlugin;

public sealed class TestPlugin : IVectorPlugin
{
    public string Id => "fixture.valid";

    public int ApiVersion => VectorPluginApi.CurrentVersion;

    public void Register(IVectorPluginContext context)
    {
        context.RegisterModule(
            new NativeModuleDefinition(
                new ModuleId(new[] { "fixture", "tools" }),
                _ => { }));
    }
}
