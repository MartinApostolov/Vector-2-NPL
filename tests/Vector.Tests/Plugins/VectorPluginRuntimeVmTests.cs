using Vector.Core.Diagnostics;
using Vector.Core.Runtime.Values;
using Vector.Plugins;
using Xunit;

namespace Vector.Tests.Plugins;

public sealed class VectorPluginRuntimeVmTests
{
    [Fact]
    public void VmEngineSharesExactNativeRegistryWithInterpreterEngineAndPlugins()
    {
        var runtime = VectorPluginRuntime.CreateDefault(
            Plugin("Vector.TestPlugin", "Vector.TestPlugin.dll"));

        Assert.Same(runtime.NativeModules, runtime.Engine.NativeModules);
        Assert.Same(runtime.NativeModules, runtime.VmEngine.NativeModules);
        Assert.Single(runtime.Plugins.Registrations);
    }

    [Fact]
    public void ExecuteVmRetainsStandardLibraryWithoutPlugins()
    {
        var runtime = VectorPluginRuntime.CreateDefault();

        var result = runtime.ExecuteVm("import lib.math; lib.math.sqrt(81);");

        Assert.True(result.Success);
        Assert.Equal(new NumberValue(9), result.Result);
        Assert.Empty(runtime.Plugins.Registrations);
    }

    [Fact]
    public void ExecuteVmCallsExplicitPluginTogetherWithStandardLibrary()
    {
        var runtime = VectorPluginRuntime.CreateDefault(
            Plugin("Vector.TestPlugin", "Vector.TestPlugin.dll"));

        var result = runtime.ExecuteVm(
            "import lib.math; import fixture.tools; " +
            "lib.math.sqrt(fixture.tools.double(8));");

        Assert.True(result.Success);
        Assert.Equal(new NumberValue(4), result.Result);
    }

    [Fact]
    public void InterpreterExecuteApiRemainsAvailableBesideExecuteVm()
    {
        var runtime = VectorPluginRuntime.CreateDefault(
            Plugin("Vector.TestPlugin", "Vector.TestPlugin.dll"));
        const string source = "import fixture.tools; fixture.tools.double(21);";

        var interpreter = runtime.Execute(source);
        var vm = runtime.ExecuteVm(source);

        Assert.True(interpreter.Success);
        Assert.True(vm.Success);
        Assert.Equal(new NumberValue(42), interpreter.Result);
        Assert.Equal(interpreter.Result, vm.Result);
    }

    [Fact]
    public void VmPluginModuleStillRequiresNormalVectorImport()
    {
        var runtime = VectorPluginRuntime.CreateDefault(
            Plugin("Vector.TestPlugin", "Vector.TestPlugin.dll"));

        var result = runtime.ExecuteVm("fixture.tools.double(21);");

        Assert.False(result.Success);
        Assert.Equal(DiagnosticCode.UndefinedVariable, Assert.Single(result.Diagnostics).Code);
    }

    [Fact]
    public void VmSourceModuleCanDependOnPluginUsingSameRuntimeRegistry()
    {
        var runtime = VectorPluginRuntime.CreateDefault(
            Plugin("Vector.TestPlugin", "Vector.TestPlugin.dll"));
        var root = Path.Combine(Path.GetTempPath(), $"vector-plugin-vm-source-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        File.WriteAllText(
            Path.Combine(root, "feature.vec"),
            "import fixture.tools; function calculate(value) { return fixture.tools.double(value) + 1; }");

        try
        {
            var result = runtime.ExecuteVm("import feature; feature.calculate(20);", root);

            Assert.True(result.Success);
            Assert.Equal(new NumberValue(41), result.Result);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void VmSourceModuleConflictWithPluginProducesStructuredDiagnostic()
    {
        var runtime = VectorPluginRuntime.CreateDefault(
            Plugin("Vector.TestPlugin", "Vector.TestPlugin.dll"));
        var root = Path.Combine(Path.GetTempPath(), $"vector-plugin-vm-conflict-{Guid.NewGuid():N}");
        var moduleDirectory = Path.Combine(root, "fixture");
        Directory.CreateDirectory(moduleDirectory);
        File.WriteAllText(Path.Combine(moduleDirectory, "tools.vec"), "let sourceValue = 1;");

        try
        {
            var result = runtime.ExecuteVm("import fixture.tools;", root);

            Assert.False(result.Success);
            Assert.Equal(DiagnosticCode.ModuleConflict, Assert.Single(result.Diagnostics).Code);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    private static string Plugin(string projectName, string assemblyName) =>
        PluginFixture.Assembly(projectName, assemblyName);
}
