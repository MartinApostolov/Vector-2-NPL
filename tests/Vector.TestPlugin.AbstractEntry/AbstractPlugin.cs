using Vector.Plugins;

namespace Vector.TestPlugin.AbstractEntry;

public abstract class AbstractPlugin : IVectorPlugin
{
    public string Id => "fixture.abstract";
    public int ApiVersion => VectorPluginApi.CurrentVersion;
    public void Register(IVectorPluginContext context) { }
}
