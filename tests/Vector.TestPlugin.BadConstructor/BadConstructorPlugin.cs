using Vector.Plugins;

namespace Vector.TestPlugin.BadConstructor;

public sealed class BadConstructorPlugin : IVectorPlugin
{
    public BadConstructorPlugin(string ignored)
    {
    }

    public string Id => "fixture.bad-constructor";
    public int ApiVersion => VectorPluginApi.CurrentVersion;
    public void Register(IVectorPluginContext context) { }
}
