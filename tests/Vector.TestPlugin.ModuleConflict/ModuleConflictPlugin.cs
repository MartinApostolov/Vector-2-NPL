using Vector.Core.Modules;
using Vector.Core.Modules.Native;
using Vector.Core.Runtime.Native;
using Vector.Plugins;

namespace Vector.TestPlugin.ModuleConflict;

public sealed class ModuleConflictPlugin : IVectorPlugin
{
    public string Id => "fixture.module-conflict";

    public int ApiVersion => VectorPluginApi.CurrentVersion;

    public void Register(IVectorPluginContext context)
    {
        // This safe module must not be committed if the later conflicting module is rejected.
        context.RegisterModule(
            new NativeModuleDefinition(
                new ModuleId(new[] { "accept", "safe" }),
                module => module.Export("value", NativeValueConverter.FromNumber(1))));

        context.RegisterModule(
            new NativeModuleDefinition(
                new ModuleId(new[] { "accept", "math" }),
                _ => { }));
    }
}
