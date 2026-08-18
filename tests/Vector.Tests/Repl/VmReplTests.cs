using Vector.Cli;
using Vector.Core.Modules.Native;
using Vector.Tests.Plugins;
using Xunit;

namespace Vector.Tests.Repl;

public sealed class VmReplTests
{
    [Fact]
    public void VmReplPreservesVariablesFunctionsClosuresAndImportsAcrossSubmissions()
    {
        var session = RunThroughCli(
            "let value = 10;\n" +
            "value = value + 5;\n" +
            "value;\n" +
            "function makeCounter() { let count = 0; function next() { count = count + 1; return count; } return next; }\n" +
            "let counter = makeCounter();\n" +
            "counter();\n" +
            "counter();\n" +
            "import lib.math;\n" +
            "lib.math.sqrt(81);\n" +
            ":exit\n");

        Assert.Equal(0, session.ExitCode);
        Assert.Contains("15", session.Output);
        Assert.Contains("1", session.Output);
        Assert.Contains("2", session.Output);
        Assert.Contains("9", session.Output);
        Assert.Empty(session.Error);
    }

    [Fact]
    public void VmReplKeepsPriorStateAfterRuntimeErrorAndContinues()
    {
        var session = Run(
            "let value = 8;\n" +
            "10 + \"bad\";\n" +
            "value;\n" +
            ":exit\n");

        Assert.Equal(0, session.ExitCode);
        Assert.Contains("RuntimeTypeError", session.Error);
        Assert.Contains("8", session.Output);
    }

    [Fact]
    public void VmReplPreservesModuleInitializationAndQualifiedVisibility()
    {
        using var program = new TemporaryProgram();
        program.WriteModule("once", "print(\"loaded\"); let answer = 42;");

        var session = Run(
            "import once;\n" +
            "once.answer;\n" +
            "import once;\n" +
            "once.answer;\n" +
            ":exit\n",
            program.Root);

        Assert.Equal(0, session.ExitCode);
        Assert.Equal(1, CountOccurrences(session.Output, "loaded"));
        Assert.True(CountOccurrences(session.Output, "42") >= 2);
        Assert.Empty(session.Error);
    }

    [Fact]
    public void VmReplUsesSameInputReaderForNativeIoCalls()
    {
        var session = Run(
            "import lib.io;\n" +
            "lib.io.readLine();\n" +
            "Ada from VM REPL\n" +
            ":exit\n");

        Assert.Equal(0, session.ExitCode);
        Assert.Contains("Ada from VM REPL", session.Output);
        Assert.Empty(session.Error);
    }

    [Fact]
    public void VmReplCombinesWithExplicitPlugins()
    {
        var plugin = PluginFixture.Assembly("Vector.TestPlugin", "Vector.TestPlugin.dll");
        var session = RunThroughCli(
            "import fixture.tools;\n" +
            "fixture.tools.double(21);\n" +
            "fixture.tools.double(5);\n" +
            ":exit\n",
            "--plugin", plugin);

        Assert.Equal(0, session.ExitCode);
        Assert.Contains("42", session.Output);
        Assert.Contains("10", session.Output);
        Assert.Empty(session.Error);
    }

    private static ReplSession Run(
        string input,
        string? programRoot = null,
        NativeModuleRegistry? nativeModules = null)
    {
        var output = new StringWriter();
        var error = new StringWriter();
        var repl = new Vector.Cli.Repl(
            new StringReader(input),
            output,
            error,
            programRoot,
            nativeModules,
            CliExecutionEngine.Vm);

        var exitCode = repl.Run();
        return new ReplSession(exitCode, output.ToString(), error.ToString());
    }

    private static ReplSession RunThroughCli(string input, params string[] extraArgs)
    {
        var output = new StringWriter();
        var error = new StringWriter();
        var args = new List<string> { "--engine", "vm" };
        args.AddRange(extraArgs);
        var exitCode = Program.Run(args, new StringReader(input), output, error);
        return new ReplSession(exitCode, output.ToString(), error.ToString());
    }

    private static int CountOccurrences(string text, string value)
    {
        var count = 0;
        var index = 0;
        while ((index = text.IndexOf(value, index, StringComparison.Ordinal)) >= 0)
        {
            count++;
            index += value.Length;
        }

        return count;
    }

    private sealed record ReplSession(int ExitCode, string Output, string Error);

    private sealed class TemporaryProgram : IDisposable
    {
        public TemporaryProgram()
        {
            Root = Path.Combine(Path.GetTempPath(), "vector-vm-repl-tests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Root);
        }

        public string Root { get; }

        public void WriteModule(string qualifiedName, string source)
        {
            var relativePath = qualifiedName.Replace('.', Path.DirectorySeparatorChar) + ".vec";
            var path = Path.Combine(Root, relativePath);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, source);
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
