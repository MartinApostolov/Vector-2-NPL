using Vector.Core.Modules;
using Vector.Core.Modules.Native;
using Vector.Plugins;

namespace Vector.TestPlugin.RegistrationFailure;

public sealed class RegistrationFailurePlugin : IVectorPlugin
{
    public string Id => "fixture.registration-failure";
    public int ApiVersion => VectorPluginApi.CurrentVersion;

    public void Register(IVectorPluginContext context)
    {
        context.RegisterModule(
            new NativeModuleDefinition(
                new ModuleId(new[] { "fixture", "staged" }),
                _ => { }));
        throw new InvalidOperationException("fixture registration exploded");
    }
}
