using Vector.Cli;
using Xunit;

namespace Vector.Tests.Repl;

public sealed class ReplTests
{
    [Fact]
    public void ReplDisplaysExpressionResult()
    {
        var session = Run("1 + 2;\n:exit\n");

        Assert.Contains("3", session.Output);
        Assert.Empty(session.Error);
    }

    [Fact]
    public void ReplDoesNotEchoVariableDeclarationResult()
    {
        var session = Run("let value = 10;\n:exit\n");

        Assert.DoesNotContain("nothing", session.Output, StringComparison.OrdinalIgnoreCase);
        Assert.Empty(session.Error);
    }

    [Fact]
    public void ReplPreservesVariablesBetweenSubmissions()
    {
        var session = Run("let value = 10;\nvalue = value + 5;\nvalue;\n:exit\n");

        Assert.Contains("15", session.Output);
        Assert.Empty(session.Error);
    }

    [Fact]
    public void ReplAllowsRuntimeTypeToChangeBetweenSubmissions()
    {
        var session = Run("let value = 10;\nvalue = \"text\";\nvalue;\n:exit\n");

        Assert.Contains("text", session.Output);
        Assert.Empty(session.Error);
    }

    [Fact]
    public void ReplPreservesFunctionsBetweenSubmissions()
    {
        var session = Run(
            "function double(value) { return value * 2; }\n" +
            "double(6);\n" +
            ":exit\n");

        Assert.Contains("12", session.Output);
        Assert.Empty(session.Error);
    }

    [Fact]
    public void ReplSupportsMultilineFunctionEntry()
    {
        var session = Run(
            "function add(a, b) {\n" +
            "    return a + b;\n" +
            "}\n" +
            "add(4, 5);\n" +
            ":exit\n");

        Assert.Contains("...> ", session.Output);
        Assert.Contains("9", session.Output);
        Assert.Empty(session.Error);
    }

    [Fact]
    public void ReplSupportsMultilineBlockEntry()
    {
        var session = Run(
            "let value = 0;\n" +
            "if true {\n" +
            "    value = 7;\n" +
            "}\n" +
            "value;\n" +
            ":exit\n");

        Assert.Contains("7", session.Output);
        Assert.Empty(session.Error);
    }

    [Fact]
    public void ReplSupportsMultilineParenthesizedExpression()
    {
        var session = Run("(1 +\n2);\n:exit\n");

        Assert.Contains("3", session.Output);
        Assert.Empty(session.Error);
    }

    [Fact]
    public void ReplSupportsMultilineListExpression()
    {
        var session = Run("[1,\n2,\n3];\n:exit\n");

        Assert.Contains("[1, 2, 3]", session.Output);
        Assert.Empty(session.Error);
    }

    [Fact]
    public void ReplPrintBuiltinWritesToConfiguredOutput()
    {
        var session = Run("print(\"hello\");\n:exit\n");

        Assert.Contains("hello", session.Output);
        Assert.Empty(session.Error);
    }

    [Fact]
    public void ReplFormatsParserDiagnosticsWithReplSourceName()
    {
        var session = Run("let = 10;\n:exit\n");

        Assert.Contains("<repl>:", session.Error);
        Assert.Contains("UnexpectedToken", session.Error);
        Assert.Contains("let = 10;", session.Error);
        Assert.Contains("^", session.Error);
    }

    [Fact]
    public void ReplFormatsRuntimeDiagnosticsAndContinues()
    {
        var session = Run("10 + \"bad\";\n2 + 3;\n:exit\n");

        Assert.Contains("RuntimeTypeError", session.Error);
        Assert.Contains("10 + \"bad\";", session.Error);
        Assert.Contains("5", session.Output);
    }

    [Fact]
    public void ReplKeepsPriorSuccessfulStateAfterRuntimeError()
    {
        var session = Run("let value = 8;\n10 + \"bad\";\nvalue;\n:exit\n");

        Assert.Contains("RuntimeTypeError", session.Error);
        Assert.Contains("8", session.Output);
    }

    [Fact]
    public void ReplSupportsQuitAliasCaseInsensitively()
    {
        var session = Run(":QUIT\n");

        Assert.Empty(session.Error);
    }

    [Fact]
    public void ReplExitsCleanlyAtEndOfInput()
    {
        var input = new StringReader(string.Empty);
        var output = new StringWriter();
        var error = new StringWriter();
        var repl = new Vector.Cli.Repl(input, output, error);

        var exitCode = repl.Run();

        Assert.Equal(0, exitCode);
        Assert.Empty(error.ToString());
    }

    [Fact]
    public void ReplExecutesFinalBufferedSubmissionAtEndOfInput()
    {
        var session = Run("4 + 5;\n");

        Assert.Contains("9", session.Output);
        Assert.Empty(session.Error);
    }

    [Fact]
    public void ReplCanImportModuleFromConfiguredProgramRoot()
    {
        using var program = new TemporaryProgram();
        program.WriteModule("lib.math", "function triple(value) { return value * 3; }");

        var session = Run(
            "import lib.math;\nlib.math.triple(4);\n:exit\n",
            program.Root);

        Assert.Contains("12", session.Output);
        Assert.Empty(session.Error);
    }

    [Fact]
    public void ReplModuleInitializationPersistsAcrossSubmissions()
    {
        using var program = new TemporaryProgram();
        program.WriteModule("once", "print(\"loaded\"); let answer = 42;");

        var session = Run(
            "import once;\nimport once;\nonce.answer;\n:exit\n",
            program.Root);

        Assert.Equal(1, CountOccurrences(session.Output, "loaded"));
        Assert.Contains("42", session.Output);
        Assert.Empty(session.Error);
    }

    [Fact]
    public void ReplReportsMissingModuleAndContinues()
    {
        using var program = new TemporaryProgram();

        var session = Run(
            "import missing;\n1 + 1;\n:exit\n",
            program.Root);

        Assert.Contains("ModuleNotFound", session.Error);
        Assert.Contains("2", session.Output);
    }

    [Fact]
    public void ReplHandlesBlankSubmissionWithoutDiagnostic()
    {
        var session = Run("\n:exit\n");

        Assert.Empty(session.Error);
    }

    private static ReplSession Run(string input, string? programRoot = null)
    {
        var output = new StringWriter();
        var error = new StringWriter();
        var repl = new Vector.Cli.Repl(new StringReader(input), output, error, programRoot);

        var exitCode = repl.Run();

        Assert.Equal(0, exitCode);
        return new ReplSession(output.ToString(), error.ToString());
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

    private sealed record ReplSession(string Output, string Error);

    private sealed class TemporaryProgram : IDisposable
    {
        public TemporaryProgram()
        {
            Root = Path.Combine(Path.GetTempPath(), "vector-repl-tests", Guid.NewGuid().ToString("N"));
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
