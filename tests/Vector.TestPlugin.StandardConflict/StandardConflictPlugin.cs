using Vector.Core.Modules;
using Vector.Core.Modules.Native;
using Vector.Plugins;

namespace Vector.TestPlugin.StandardConflict;

public sealed class StandardConflictPlugin : IVectorPlugin
{
    public string Id => "fixture.standard-conflict";

    public int ApiVersion => VectorPluginApi.CurrentVersion;

    public void Register(IVectorPluginContext context)
    {
        context.RegisterModule(
            new NativeModuleDefinition(
                new ModuleId(new[] { "lib", "math" }),
                _ => { }));
    }
}
