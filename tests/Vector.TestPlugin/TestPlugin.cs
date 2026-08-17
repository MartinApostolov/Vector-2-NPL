using System.Reflection;
using Vector.Core.Modules;
using Vector.Core.Modules.Native;
using Vector.Core.Runtime.Native;
using Vector.Plugins;
using Vector.TestPlugin.Dependency;

namespace Vector.TestPlugin;

public sealed class TestPlugin : IVectorPlugin
{
    public string Id => "fixture.valid";

    public int ApiVersion => VectorPluginApi.CurrentVersion;

    public Assembly CoreAssembly => typeof(NativeModuleDefinition).Assembly;

    public Assembly PluginContractAssembly => typeof(IVectorPlugin).Assembly;

    public Assembly DependencyAssembly => typeof(DependencyHelper).Assembly;

    public void Register(IVectorPluginContext context)
    {
        context.RegisterModule(
            new NativeModuleDefinition(
                new ModuleId(new[] { "fixture", "tools" }),
                module => module.Export(
                    "double",
                    new NativeFunction(
                        "double",
                        1,
                        (_, arguments) =>
                        {
                            var value = NativeValueConverter.ToNumber(arguments[0], "value");
                            return NativeValueConverter.FromNumber(DependencyHelper.Double(value));
                        }))));
    }
}
