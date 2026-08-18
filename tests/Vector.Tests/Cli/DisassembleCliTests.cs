using Vector.Cli;
using Xunit;

namespace Vector.Tests.Cli;

public sealed class DisassembleCliTests
{
    [Fact]
    public void VmDisassemblePrintsBytecodeWithoutExecutingProgramSideEffects()
    {
        using var program = new TemporaryProgram();
        var source = program.Write(
            "disassemble.vec",
            "print(\"SIDE_EFFECT_ONLY\"); let value = 1 + 2; value;");

        var session = Run("--engine", "vm", "--disassemble", source);

        Assert.Equal(0, session.ExitCode);
        Assert.Contains("== <script> ==", session.Output);
        Assert.Contains("Add", session.Output);
        Assert.Contains("Call", session.Output);
        Assert.Contains(Path.GetFullPath(source), session.Output);
        Assert.DoesNotContain(
            session.Output.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries),
            line => string.Equals(line.Trim(), "SIDE_EFFECT_ONLY", StringComparison.Ordinal));
        Assert.Empty(session.Error);
    }

    [Fact]
    public void DisassembleReturnsLanguageFailureForInvalidSource()
    {
        using var program = new TemporaryProgram();
        var source = program.Write("broken.vec", "let value = ;");

        var session = Run("--engine", "vm", "--disassemble", source);

        Assert.Equal(1, session.ExitCode);
        Assert.Empty(session.Output);
        Assert.Contains(Path.GetFileName(source), session.Error);
        Assert.Contains("error", session.Error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void DisassembleRequiresVmBackend()
    {
        using var program = new TemporaryProgram();
        var source = program.Write("program.vec", "1 + 2;");

        var session = Run("--disassemble", source);

        Assert.Equal(2, session.ExitCode);
        Assert.Contains("requires '--engine vm'", session.Error);
    }

    [Fact]
    public void DisassembleRequiresSourceFile()
    {
        var session = Run("--engine", "vm", "--disassemble");

        Assert.Equal(2, session.ExitCode);
        Assert.Contains("requires a Vector source file", session.Error);
    }

    private static CliSession Run(params string[] args)
    {
        var output = new StringWriter();
        var error = new StringWriter();
        var exitCode = Program.Run(args, new StringReader(string.Empty), output, error);
        return new CliSession(exitCode, output.ToString(), error.ToString());
    }

    private sealed record CliSession(int ExitCode, string Output, string Error);

    private sealed class TemporaryProgram : IDisposable
    {
        public TemporaryProgram()
        {
            Root = Path.Combine(Path.GetTempPath(), "vector-disassemble-cli-tests", Guid.NewGuid().ToString("N"));
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
