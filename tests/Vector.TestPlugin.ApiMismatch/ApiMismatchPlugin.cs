using Vector.Plugins;

namespace Vector.TestPlugin.ApiMismatch;

public sealed class ApiMismatchPlugin : IVectorPlugin
{
    public string Id => "fixture.api-mismatch";
    public int ApiVersion => VectorPluginApi.CurrentVersion + 1;
    public void Register(IVectorPluginContext context) { }
}
