using Vector.Plugins;

namespace Vector.TestPlugin.ThrowingConstructor;

public sealed class ThrowingConstructorPlugin : IVectorPlugin
{
    public ThrowingConstructorPlugin()
    {
        throw new InvalidOperationException("fixture constructor exploded");
    }

    public string Id => "fixture.throwing-constructor";
    public int ApiVersion => VectorPluginApi.CurrentVersion;
    public void Register(IVectorPluginContext context) { }
}
