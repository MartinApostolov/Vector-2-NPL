using Vector.Core.Modules;
using Vector.Core.Modules.Native;
using Vector.Plugins;

namespace Vector.TestPlugin.DuplicateModule;

public sealed class DuplicateModulePlugin : IVectorPlugin
{
    public string Id => "fixture.duplicate-module";

    public int ApiVersion => VectorPluginApi.CurrentVersion;

    public void Register(IVectorPluginContext context)
    {
        var id = new ModuleId(new[] { "accept", "duplicate" });
        context.RegisterModule(new NativeModuleDefinition(id, _ => { }));
        context.RegisterModule(new NativeModuleDefinition(id, _ => { }));
    }
}
