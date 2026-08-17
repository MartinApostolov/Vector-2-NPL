using System.Reflection;
using System.Runtime.Loader;
using Vector.Core;
using Vector.Core.Modules.Native;
using Vector.Plugins;
using Vector.Plugins.Loading;
using Xunit;

namespace Vector.Tests.Plugins;

public sealed class PluginDependencyTests
{
    [Fact]
    public void PluginLocalManagedDependencyLoadsAndExecutesHelperCode()
    {
        var registry = new NativeModuleRegistry();
        var manager = new VectorPluginManager(registry);
        manager.LoadFromPath(Fixture("Vector.TestPlugin", "Vector.TestPlugin.dll"));

        var result = new VectorEngine(registry).Execute(
            "import fixture.tools;\nprint(fixture.tools.double(21));");

        Assert.True(result.Success);
        Assert.Equal("42", Assert.Single(result.Output));
    }

    [Fact]
    public void PluginUsesHostVectorAssembliesButKeepsPrivateDependencyInPluginContext()
    {
        var plugin = new VectorPluginLoader().LoadFromPath(Fixture("Vector.TestPlugin", "Vector.TestPlugin.dll"));
        var pluginAssembly = plugin.GetType().Assembly;
        var pluginContext = AssemblyLoadContext.GetLoadContext(pluginAssembly);

        Assert.NotNull(pluginContext);
        Assert.NotSame(AssemblyLoadContext.Default, pluginContext);

        var coreAssembly = ReadAssemblyProperty(plugin, "CoreAssembly");
        var contractAssembly = ReadAssemblyProperty(plugin, "PluginContractAssembly");
        var dependencyAssembly = ReadAssemblyProperty(plugin, "DependencyAssembly");

        Assert.Same(typeof(NativeModuleDefinition).Assembly, coreAssembly);
        Assert.Same(typeof(IVectorPlugin).Assembly, contractAssembly);
        Assert.Same(pluginContext, AssemblyLoadContext.GetLoadContext(dependencyAssembly));
        Assert.NotSame(AssemblyLoadContext.Default, AssemblyLoadContext.GetLoadContext(dependencyAssembly));
    }

    [Fact]
    public void MissingPluginLocalManagedDependencyFailsAsStructuredLoadError()
    {
        var sourcePlugin = Fixture("Vector.TestPlugin", "Vector.TestPlugin.dll");
        var sourceDirectory = Path.GetDirectoryName(sourcePlugin)!;
        var temporaryDirectory = Path.Combine(
            Path.GetTempPath(),
            $"vector-plugin-missing-dependency-{Guid.NewGuid():N}");
        Directory.CreateDirectory(temporaryDirectory);

        var copiedPlugin = Path.Combine(temporaryDirectory, Path.GetFileName(sourcePlugin));
        File.Copy(sourcePlugin, copiedPlugin);

        var dependencyManifest = Path.Combine(sourceDirectory, "Vector.TestPlugin.deps.json");
        if (File.Exists(dependencyManifest))
        {
            File.Copy(
                dependencyManifest,
                Path.Combine(temporaryDirectory, Path.GetFileName(dependencyManifest)));
        }

        try
        {
            var manager = new VectorPluginManager(new NativeModuleRegistry());

            var error = Assert.Throws<VectorPluginLoadException>(() =>
                manager.LoadFromPath(copiedPlugin));

            Assert.Equal(VectorPluginLoadErrorKind.AssemblyLoadFailure, error.ErrorKind);
            Assert.Equal(Path.GetFullPath(copiedPlugin), error.PluginPath);
            Assert.NotNull(error.InnerException);
            Assert.Empty(manager.Registrations);
        }
        finally
        {
            TryDeleteDirectory(temporaryDirectory);
        }
    }

    private static Assembly ReadAssemblyProperty(IVectorPlugin plugin, string propertyName) =>
        Assert.IsAssignableFrom<Assembly>(plugin.GetType().GetProperty(propertyName)!.GetValue(plugin));

    private static string Fixture(string projectName, string assemblyName) =>
        PluginFixture.Assembly(projectName, assemblyName);

    private static void TryDeleteDirectory(string path)
    {
        try
        {
            Directory.Delete(path, recursive: true);
        }
        catch (IOException)
        {
            // Non-collectible plugin contexts may keep assembly files mapped until process exit.
        }
        catch (UnauthorizedAccessException)
        {
            // Same best-effort cleanup rule on Windows.
        }
    }
}
