using Vector.Cli;
using Vector.Tests.Plugins;
using Xunit;

namespace Vector.Tests.Cli;

public sealed class VmCliTests
{
    [Fact]
    public void DefaultBackendRemainsInterpreterAndExplicitBackendsMatch()
    {
        using var program = new TemporaryProgram();
        var source = program.Write(
            "engines.vec",
            "let values = [1, 2, 3]; values[1] = 10; print(values * 2);");

        var defaultRun = Run(string.Empty, source);
        var interpreterRun = Run(string.Empty, "--engine", "interpreter", source);
        var vmRun = Run(string.Empty, "--engine", "vm", source);

        Assert.Equal(0, defaultRun.ExitCode);
        Assert.Equal(0, interpreterRun.ExitCode);
        Assert.Equal(0, vmRun.ExitCode);
        Assert.Equal(interpreterRun.Output, defaultRun.Output);
        Assert.Equal(interpreterRun.Output, vmRun.Output);
        Assert.Contains("[2, 20, 6]", vmRun.Output);
        Assert.Empty(defaultRun.Error);
        Assert.Empty(interpreterRun.Error);
        Assert.Empty(vmRun.Error);
    }

    [Fact]
    public void VmBackendExecutesLocalSourceModules()
    {
        using var program = new TemporaryProgram();
        program.Write("feature.vec", "function next(value) { return value + 1; }");
        var source = program.Write(
            "main.vec",
            "import feature; print(feature.next(41));");

        var session = Run(string.Empty, "--engine", "vm", source);

        Assert.Equal(0, session.ExitCode);
        Assert.Contains("42", session.Output);
        Assert.Empty(session.Error);
    }

    [Fact]
    public void VmBackendCombinesWithExplicitPluginOption()
    {
        using var program = new TemporaryProgram();
        var source = program.Write(
            "plugin-vm.vec",
            "import fixture.tools; print(fixture.tools.double(21));");
        var plugin = PluginFixture.Assembly("Vector.TestPlugin", "Vector.TestPlugin.dll");

        var session = Run(
            string.Empty,
            "--engine", "vm",
            "--plugin", plugin,
            source);

        Assert.Equal(0, session.ExitCode);
        Assert.Contains("42", session.Output);
        Assert.Empty(session.Error);
    }

    [Fact]
    public void InvalidEngineIsCommandLineFailure()
    {
        var session = Run(string.Empty, "--engine", "jit");

        Assert.Equal(2, session.ExitCode);
        Assert.Contains("invalid engine 'jit'", session.Error);
        Assert.Contains("interpreter", session.Error);
        Assert.Contains("vm", session.Error);
        Assert.Contains("Usage: vector", session.Error);
    }

    [Fact]
    public void EngineOptionRequiresFollowingBackendName()
    {
        var session = Run(string.Empty, "--engine");

        Assert.Equal(2, session.ExitCode);
        Assert.Contains("option '--engine' requires", session.Error);
        Assert.Contains("Usage: vector", session.Error);
    }

    [Fact]
    public void EngineOptionMayOnlyBeSpecifiedOnce()
    {
        var session = Run(string.Empty, "--engine", "vm", "--engine", "interpreter");

        Assert.Equal(2, session.ExitCode);
        Assert.Contains("may only be supplied once", session.Error);
    }

    private static CliSession Run(string input, params string[] args)
    {
        var output = new StringWriter();
        var error = new StringWriter();
        var exitCode = Program.Run(args, new StringReader(input), output, error);
        return new CliSession(exitCode, output.ToString(), error.ToString());
    }

    private sealed record CliSession(int ExitCode, string Output, string Error);

    private sealed class TemporaryProgram : IDisposable
    {
        public TemporaryProgram()
        {
            Root = Path.Combine(Path.GetTempPath(), "vector-vm-cli-tests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Root);
        }

        public string Root { get; }

        public string Write(string fileName, string source)
        {
            var path = Path.Combine(Root, fileName);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, source);
            return path;
        }

        public void Dispose()
        {
            try
            {
                Directory.Delete(Root, recursive: true);
            }
            catch
            {
                // Best-effort test cleanup.
            }
        }
    }
}
