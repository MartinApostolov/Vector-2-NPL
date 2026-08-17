using Vector.Cli;
using Vector.Tests.Plugins;
using Xunit;

namespace Vector.Tests.Cli;

public sealed class PluginCliTests
{
    [Fact]
    public void ExistingFileExecutionStillWorksWithoutPlugins()
    {
        using var program = new TemporaryProgram();
        var source = program.Write("plain.vec", "print(1 + 2);");

        var session = Run(string.Empty, source);

        Assert.Equal(0, session.ExitCode);
        Assert.Contains("3", session.Output);
        Assert.Empty(session.Error);
    }

    [Fact]
    public void OneExplicitPluginCanBeUsedByFileExecution()
    {
        using var program = new TemporaryProgram();
        var source = program.Write(
            "plugin.vec",
            "import fixture.tools;\nprint(fixture.tools.double(21));");

        var session = Run(
            string.Empty,
            "--plugin",
            Plugin("Vector.TestPlugin", "Vector.TestPlugin.dll"),
            source);

        Assert.Equal(0, session.ExitCode);
        Assert.Contains("42", session.Output);
        Assert.Empty(session.Error);
    }

    [Fact]
    public void MultipleExplicitPluginsCanBeUsedByFileExecution()
    {
        using var program = new TemporaryProgram();
        var source = program.Write(
            "plugins.vec",
            "import fixture.tools;\n" +
            "import fixture.extra;\n" +
            "print(fixture.tools.double(21) + fixture.extra.increment(1));");

        var session = Run(
            string.Empty,
            "--plugin",
            Plugin("Vector.TestPlugin", "Vector.TestPlugin.dll"),
            "--plugin",
            Plugin("Vector.TestPlugin.Second", "Vector.TestPlugin.Second.dll"),
            source);

        Assert.Equal(0, session.ExitCode);
        Assert.Contains("44", session.Output);
        Assert.Empty(session.Error);
    }

    [Fact]
    public void PluginOptionRequiresFollowingPath()
    {
        var session = Run(string.Empty, "--plugin");

        Assert.Equal(2, session.ExitCode);
        Assert.Contains("requires a following DLL path", session.Error);
        Assert.Contains("Usage: vector", session.Error);
    }

    [Fact]
    public void UnknownOptionIsCommandLineFailure()
    {
        var session = Run(string.Empty, "--unknown");

        Assert.Equal(2, session.ExitCode);
        Assert.Contains("unknown option '--unknown'", session.Error);
    }

    [Fact]
    public void MultipleSourceFilesAreRejected()
    {
        using var program = new TemporaryProgram();
        var first = program.Write("first.vec", "1;");
        var second = program.Write("second.vec", "2;");

        var session = Run(string.Empty, first, second);

        Assert.Equal(2, session.ExitCode);
        Assert.Contains("at most one Vector source file", session.Error);
    }

    [Fact]
    public void MissingSourceFileRemainsCommandLineFailure()
    {
        var missing = Path.Combine(Path.GetTempPath(), $"vector-cli-missing-{Guid.NewGuid():N}.vec");

        var session = Run(string.Empty, missing);

        Assert.Equal(2, session.ExitCode);
        Assert.Contains("cannot read", session.Error);
        Assert.Contains(Path.GetFileName(missing), session.Error);
    }

    [Fact]
    public void IncompatiblePluginIsConciseSetupFailure()
    {
        var plugin = Plugin("Vector.TestPlugin.ApiMismatch", "Vector.TestPlugin.ApiMismatch.dll");

        var session = Run(string.Empty, "--plugin", plugin);

        Assert.Equal(2, session.ExitCode);
        Assert.Contains("could not be registered", session.Error);
        Assert.Contains("API version", session.Error, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Stack trace", session.Error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void DuplicatePluginIsSetupFailureBeforeReplStarts()
    {
        var plugin = Plugin("Vector.TestPlugin", "Vector.TestPlugin.dll");

        var session = Run(":exit\n", "--plugin", plugin, "--plugin", plugin);

        Assert.Equal(2, session.ExitCode);
        Assert.Contains("could not be registered", session.Error);
        Assert.Contains("already registered", session.Error);
        Assert.DoesNotContain("Vector REPL", session.Output);
    }

    [Fact]
    public void PluginModuleConflictIsSetupFailure()
    {
        var plugin = Plugin(
            "Vector.TestPlugin.StandardConflict",
            "Vector.TestPlugin.StandardConflict.dll");

        var session = Run(string.Empty, "--plugin", plugin);

        Assert.Equal(2, session.ExitCode);
        Assert.Contains("could not be registered", session.Error);
        Assert.Contains("lib.math", session.Error);
    }

    private static CliSession Run(string input, params string[] args)
    {
        var output = new StringWriter();
        var error = new StringWriter();
        var exitCode = Program.Run(args, new StringReader(input), output, error);
        return new CliSession(exitCode, output.ToString(), error.ToString());
    }

    private static string Plugin(string projectName, string assemblyName) =>
        PluginFixture.Assembly(projectName, assemblyName);

    private sealed record CliSession(int ExitCode, string Output, string Error);

    private sealed class TemporaryProgram : IDisposable
    {
        public TemporaryProgram()
        {
            Root = Path.Combine(Path.GetTempPath(), "vector-cli-plugin-tests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Root);
        }

        public string Root { get; }

        public string Write(string fileName, string source)
        {
            var path = Path.Combine(Root, fileName);
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
