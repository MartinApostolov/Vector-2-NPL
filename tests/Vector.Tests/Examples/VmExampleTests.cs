using Vector.Core;
using Vector.Plugins;
using Xunit;

namespace Vector.Tests.Examples;

public sealed class VmExampleTests
{
    public static IEnumerable<object[]> Programs =>
        new[]
        {
            Case("examples/01_hello.vec"), Case("examples/02_variables.vec"), Case("examples/03_conditions.vec"),
            Case("examples/04_while_loop.vec"), Case("examples/05_for_loop.vec"), Case("examples/06_functions.vec"),
            Case("examples/07_lists.vec"), Case("examples/08_vectors.vec"), Case("examples/09_scopes.vec"),
            Case("examples/10_modules/main.vec"), Case("examples/11_native_math.vec"), Case("examples/12_standard_library.vec"),
            Case("examples/13_vector_math.vec"), Case("examples/14_matrix_math.vec")
        };

    [Theory]
    [MemberData(nameof(Programs))]
    public void EveryNonPluginExampleMatchesInterpreter(string relativePath)
    {
        var path = Path.Combine(FindRepositoryRoot(), relativePath.Replace('/', Path.DirectorySeparatorChar));
        var source = File.ReadAllText(path);
        var root = Path.GetDirectoryName(path)!;

        var interpreter = new VectorEngine().Execute(source, root);
        var vm = new VectorVmEngine().Execute(source, root);

        Assert.True(interpreter.Success);
        Assert.True(vm.Success);
        Assert.Equal(interpreter.Result, vm.Result);
        Assert.Equal(interpreter.Output, vm.Output);
    }

    [Fact]
    public void ExternalPluginExampleMatchesInterpreter()
    {
        var root = FindRepositoryRoot();
        var programPath = Path.Combine(root, "examples", "15_external_plugin", "main.vec");
        var runtime = VectorPluginRuntime.CreateDefault(ExamplePluginAssembly(root));
        var source = File.ReadAllText(programPath);
        var programRoot = Path.GetDirectoryName(programPath)!;

        var interpreter = runtime.Execute(source, programRoot);
        var vm = runtime.ExecuteVm(source, programRoot);

        Assert.True(interpreter.Success);
        Assert.True(vm.Success);
        Assert.Equal(interpreter.Result, vm.Result);
        Assert.Equal(interpreter.Output, vm.Output);
    }

    private static object[] Case(string path) => new object[] { path };

    private static string ExamplePluginAssembly(string root)
    {
        var configuration = new DirectoryInfo(AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar)).Parent?.Name ?? "Debug";
        return Path.Combine(root, "examples", "plugins", "Vector.ExamplePlugin", "bin", configuration, "net8.0", "Vector.ExamplePlugin.dll");
    }

    private static string FindRepositoryRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            if (File.Exists(Path.Combine(directory.FullName, "Vector.sln"))) return directory.FullName;
        }
        throw new DirectoryNotFoundException("Could not locate Vector.sln.");
    }
}
