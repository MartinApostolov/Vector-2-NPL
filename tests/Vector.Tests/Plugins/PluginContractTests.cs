using Vector.Core.Modules;
using Vector.Core.Modules.Native;
using Vector.Plugins;
using Xunit;

namespace Vector.Tests.Plugins;

public sealed class PluginContractTests
{
    [Fact]
    public void CurrentApiVersionIsNonZero()
    {
        Assert.True(VectorPluginApi.CurrentVersion > 0);
        Assert.Equal(1, VectorPluginApi.CurrentVersion);
    }

    [Fact]
    public void ContextStagesOneModule()
    {
        var context = new VectorPluginContext();
        var definition = Definition("example.tools");

        context.RegisterModule(definition);

        Assert.Same(definition, Assert.Single(context.StagedModules));
    }

    [Fact]
    public void ContextStagesSeveralModules()
    {
        var context = new VectorPluginContext();
        var tools = Definition("example.tools");
        var text = Definition("example.text");

        context.RegisterModule(tools);
        context.RegisterModule(text);

        Assert.Equal(2, context.StagedModules.Count);
        Assert.Same(tools, context.StagedModules[0]);
        Assert.Same(text, context.StagedModules[1]);
    }

    [Fact]
    public void ContextRejectsNullModule()
    {
        var context = new VectorPluginContext();

        Assert.Throws<ArgumentNullException>(() => context.RegisterModule(null!));
        Assert.Empty(context.StagedModules);
    }

    [Fact]
    public void ContextRejectsDuplicateModuleId()
    {
        var context = new VectorPluginContext();
        var first = Definition("example.tools");
        var duplicate = Definition("example.tools");
        context.RegisterModule(first);

        var error = Assert.Throws<InvalidOperationException>(() =>
            context.RegisterModule(duplicate));

        Assert.Contains("example.tools", error.Message);
        Assert.Same(first, Assert.Single(context.StagedModules));
    }

    [Fact]
    public void StagingDoesNotMutateNativeModuleRegistry()
    {
        var registry = new NativeModuleRegistry();
        var context = new VectorPluginContext();
        var definition = Definition("example.tools");

        context.RegisterModule(definition);

        Assert.Empty(registry.Definitions);
        Assert.False(registry.TryGet(Id("example.tools"), out var resolved));
        Assert.Null(resolved);
        Assert.Same(definition, Assert.Single(context.StagedModules));
    }

    [Fact]
    public void PluginContractCanDeclareStableIdentityAndRegisterModules()
    {
        IVectorPlugin plugin = new TestPlugin();
        var context = new VectorPluginContext();

        plugin.Register(context);

        Assert.Equal("example.plugin", plugin.Id);
        Assert.Equal(VectorPluginApi.CurrentVersion, plugin.ApiVersion);
        Assert.Equal("example.tools", Assert.Single(context.StagedModules).Id.QualifiedName);
    }

    private static NativeModuleDefinition Definition(string qualifiedName) =>
        new(Id(qualifiedName), _ => { });

    private static ModuleId Id(string qualifiedName) =>
        new(qualifiedName.Split('.', StringSplitOptions.RemoveEmptyEntries));

    private sealed class TestPlugin : IVectorPlugin
    {
        public string Id => "example.plugin";

        public int ApiVersion => VectorPluginApi.CurrentVersion;

        public void Register(IVectorPluginContext context)
        {
            context.RegisterModule(Definition("example.tools"));
        }
    }
}
