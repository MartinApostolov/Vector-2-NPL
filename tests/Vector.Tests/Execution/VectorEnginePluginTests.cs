using Vector.Core.Diagnostics;
using Vector.Core.Runtime.Host;
using Vector.Core.Runtime.Values;
using Vector.Plugins;
using Vector.Tests.Plugins;
using Xunit;

namespace Vector.Tests.Execution;

public sealed class VectorEnginePluginTests
{
    [Fact]
    public void RuntimeManagerAndEngineSharePluginModulesForTheirLifetime()
    {
        var runtime = VectorPluginRuntime.CreateDefault();
        runtime.Plugins.LoadFromPath(Plugin("Vector.TestPlugin", "Vector.TestPlugin.dll"));

        var result = runtime.Engine.Execute(
            "import fixture.tools;\nprint(fixture.tools.double(5));");

        Assert.True(result.Success);
        Assert.Equal("10", Assert.Single(result.Output));
    }

    [Fact]
    public void RuntimeEngineExecutesPluginModuleThroughSharedRegistry()
    {
        var runtime = VectorPluginRuntime.CreateDefault(Plugin("Vector.TestPlugin", "Vector.TestPlugin.dll"));

        var result = runtime.Engine.Execute(
            "import fixture.tools;\nlet value = fixture.tools.double(7);\nvalue;");

        Assert.True(result.Success);
        Assert.Equal(new NumberValue(14), result.Result);
        Assert.Same(runtime.NativeModules, runtime.Engine.NativeModules);
    }

    [Fact]
    public void RuntimeConvenienceExecuteCallsPluginFunctionNormally()
    {
        var runtime = VectorPluginRuntime.CreateDefault(Plugin("Vector.TestPlugin.Second", "Vector.TestPlugin.Second.dll"));

        var result = runtime.Execute(
            "import fixture.extra;\nprint(fixture.extra.increment(41));");

        Assert.True(result.Success);
        Assert.Equal("42", Assert.Single(result.Output));
    }

    [Fact]
    public void RuntimePreservesExistingHostInputAndOutputBehavior()
    {
        var runtime = VectorPluginRuntime.CreateDefault(Plugin("Vector.TestPlugin", "Vector.TestPlugin.dll"));
        var forwarded = new List<string>();
        var host = new VectorInputHost(forwarded.Add, () => "21");

        var result = runtime.Execute(
            "import lib.io;\n" +
            "import fixture.tools;\n" +
            "let value = number(lib.io.readLine());\n" +
            "print(fixture.tools.double(value));",
            host: host);

        Assert.True(result.Success);
        Assert.Equal("42", Assert.Single(result.Output));
        Assert.Equal(result.Output, forwarded);
    }

    [Fact]
    public void FreshDefaultEngineStillDoesNotSeePluginWithoutRuntimeSetup()
    {
        var result = new Vector.Core.VectorEngine().Execute("import fixture.tools;");

        Assert.False(result.Success);
        Assert.Equal(DiagnosticCode.ModuleNotFound, Assert.Single(result.Diagnostics).Code);
    }

    private static string Plugin(string projectName, string assemblyName) =>
        PluginFixture.Assembly(projectName, assemblyName);
}
