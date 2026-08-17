using System.Reflection;
using Vector.Plugins;
using Vector.Plugins.Loading;
using Xunit;

namespace Vector.Tests.Examples;

public sealed class ExternalPluginExampleTests
{
    [Fact]
    public void ExamplePluginAssemblyLoadsWithExpectedIdentity()
    {
        var plugin = new VectorPluginLoader().LoadFromPath(ExamplePluginAssembly());

        Assert.Equal("example.tools.plugin", plugin.Id);
        Assert.Equal(VectorPluginApi.CurrentVersion, plugin.ApiVersion);
    }

    [Fact]
    public void ExamplePluginProgramProducesDocumentedOutput()
    {
        var root = FindRepositoryRoot();
        var programPath = Path.Combine(root, "examples", "15_external_plugin", "main.vec");
        var runtime = VectorPluginRuntime.CreateDefault(ExamplePluginAssembly());

        var result = runtime.Execute(
            File.ReadAllText(programPath),
            Path.GetDirectoryName(programPath));

        Assert.True(result.Success);
        Assert.Equal(new[] { "42", "42", "Hello, Vector!" }, result.Output);
    }

    [Fact]
    public void ExamplePluginHasNoTestOnlyAssemblyDependencies()
    {
        var plugin = new VectorPluginLoader().LoadFromPath(ExamplePluginAssembly());
        var references = plugin.GetType().Assembly
            .GetReferencedAssemblies()
            .Select(reference => reference.Name)
            .Where(name => name is not null)
            .Cast<string>()
            .ToArray();

        Assert.DoesNotContain(references, name => name.StartsWith("Vector.Test", StringComparison.Ordinal));
        Assert.DoesNotContain(references, name => name.StartsWith("xunit", StringComparison.OrdinalIgnoreCase));
    }

    private static string ExamplePluginAssembly()
    {
        var root = FindRepositoryRoot();
        var configuration = GetBuildConfiguration();
        var path = Path.Combine(
            root,
            "examples",
            "plugins",
            "Vector.ExamplePlugin",
            "bin",
            configuration,
            "net8.0",
            "Vector.ExamplePlugin.dll");

        if (!File.Exists(path))
        {
            throw new FileNotFoundException(
                $"Example plugin assembly '{path}' was not built.",
                path);
        }

        return path;
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "Vector.sln")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException(
            $"Could not locate Vector.sln above test output '{AppContext.BaseDirectory}'.");
    }

    private static string GetBuildConfiguration()
    {
        var output = new DirectoryInfo(AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar));
        var configuration = output.Parent?.Name;
        return string.IsNullOrWhiteSpace(configuration) ? "Debug" : configuration;
    }
}
