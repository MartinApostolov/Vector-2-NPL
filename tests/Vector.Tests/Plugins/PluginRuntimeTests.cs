using Vector.Core.Diagnostics;
using Vector.Plugins;
using Vector.Plugins.Loading;
using Xunit;

namespace Vector.Tests.Plugins;

public sealed class PluginRuntimeTests
{
    [Fact]
    public void CreateDefaultWithoutPluginsRetainsStandardLibrary()
    {
        var runtime = VectorPluginRuntime.CreateDefault();

        var result = runtime.Execute("import lib.math;\nprint(lib.math.sqrt(81));");

        Assert.True(result.Success);
        Assert.Equal("9", Assert.Single(result.Output));
        Assert.Empty(runtime.Plugins.Registrations);
    }

    [Fact]
    public void CreateDefaultIncludesStandardLibraryAndExplicitPlugin()
    {
        var runtime = VectorPluginRuntime.CreateDefault(Plugin("Vector.TestPlugin", "Vector.TestPlugin.dll"));

        var result = runtime.Execute(
            "import lib.math;\nimport fixture.tools;\nprint(lib.math.sqrt(fixture.tools.double(8)));");

        Assert.True(result.Success);
        Assert.Equal("4", Assert.Single(result.Output));
        Assert.Single(runtime.Plugins.Registrations);
        Assert.Equal("fixture.valid", runtime.Plugins.Registrations[0].Id);
    }

    [Fact]
    public void CreateDefaultLoadsMultipleExplicitPluginsIntoSameRuntime()
    {
        var runtime = VectorPluginRuntime.CreateDefault(
            Plugin("Vector.TestPlugin", "Vector.TestPlugin.dll"),
            Plugin("Vector.TestPlugin.Second", "Vector.TestPlugin.Second.dll"));

        var result = runtime.Execute(
            "import fixture.tools;\n" +
            "import fixture.extra;\n" +
            "print(fixture.tools.double(10) + fixture.extra.increment(1));");

        Assert.True(result.Success);
        Assert.Equal("22", Assert.Single(result.Output));
        Assert.Equal(2, runtime.Plugins.Registrations.Count);
        Assert.Contains(runtime.Plugins.Registrations, registration => registration.Id == "fixture.valid");
        Assert.Contains(runtime.Plugins.Registrations, registration => registration.Id == "fixture.second");
    }

    [Fact]
    public void PluginModuleStillRequiresNormalVectorImport()
    {
        var runtime = VectorPluginRuntime.CreateDefault(Plugin("Vector.TestPlugin", "Vector.TestPlugin.dll"));

        var result = runtime.Execute("fixture.tools.double(21);");

        Assert.False(result.Success);
        var diagnostic = Assert.Single(result.Diagnostics);
        Assert.Equal(DiagnosticCode.UndefinedVariable, diagnostic.Code);
    }

    [Fact]
    public void SourceModuleConflictWithPluginModuleStillProducesStructuredDiagnostic()
    {
        var runtime = VectorPluginRuntime.CreateDefault(Plugin("Vector.TestPlugin", "Vector.TestPlugin.dll"));
        var root = Path.Combine(Path.GetTempPath(), $"vector-plugin-runtime-conflict-{Guid.NewGuid():N}");
        var moduleDirectory = Path.Combine(root, "fixture");
        Directory.CreateDirectory(moduleDirectory);
        File.WriteAllText(Path.Combine(moduleDirectory, "tools.vec"), "let sourceValue = 1;");

        try
        {
            var result = runtime.Execute("import fixture.tools;", root);

            Assert.False(result.Success);
            var diagnostic = Assert.Single(result.Diagnostics);
            Assert.Equal(DiagnosticCode.ModuleConflict, diagnostic.Code);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void PluginLoadFailurePreventsDefaultRuntimeFromBeingReturned()
    {
        var missing = Path.Combine(Path.GetTempPath(), $"vector-plugin-runtime-missing-{Guid.NewGuid():N}.dll");

        var error = Assert.Throws<VectorPluginLoadException>(() =>
            VectorPluginRuntime.CreateDefault(
                Plugin("Vector.TestPlugin", "Vector.TestPlugin.dll"),
                missing));

        Assert.Equal(VectorPluginLoadErrorKind.FileNotFound, error.ErrorKind);
    }

    private static string Plugin(string projectName, string assemblyName) =>
        PluginFixture.Assembly(projectName, assemblyName);
}
