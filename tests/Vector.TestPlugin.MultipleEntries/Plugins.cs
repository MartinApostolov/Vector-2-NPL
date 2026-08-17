using Vector.Plugins;

namespace Vector.TestPlugin.MultipleEntries;

public sealed class FirstPlugin : IVectorPlugin
{
    public string Id => "fixture.first";
    public int ApiVersion => VectorPluginApi.CurrentVersion;
    public void Register(IVectorPluginContext context) { }
}

public sealed class SecondPlugin : IVectorPlugin
{
    public string Id => "fixture.second";
    public int ApiVersion => VectorPluginApi.CurrentVersion;
    public void Register(IVectorPluginContext context) { }
}
