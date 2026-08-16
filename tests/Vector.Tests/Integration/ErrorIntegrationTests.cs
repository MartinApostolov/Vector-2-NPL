using Vector.Cli;
using Vector.Core;
using Vector.Core.Diagnostics;
using Xunit;

namespace Vector.Tests.Integration;

public sealed class ErrorIntegrationTests
{
    [Fact]
    public void StrictTypeFailureIsStructuredEndToEnd()
    {
        var result = new VectorEngine().Execute("10 + \"bad\";");

        Assert.False(result.Success);
        Assert.Equal(DiagnosticCode.RuntimeTypeError, Assert.Single(result.Diagnostics).Code);
    }

    [Fact]
    public void NonBooleanConditionFailsWithoutTruthiness()
    {
        var result = new VectorEngine().Execute("if 1 { print(\"bad\"); }");

        Assert.False(result.Success);
        Assert.Equal(DiagnosticCode.RuntimeTypeError, Assert.Single(result.Diagnostics).Code);
    }

    [Fact]
    public void WrongArgumentCountIsStructuredEndToEnd()
    {
        var result = new VectorEngine().Execute(
            "function add(a, b) { return a + b; } add(1);");

        Assert.False(result.Success);
        Assert.Equal(DiagnosticCode.ArgumentCountMismatch, Assert.Single(result.Diagnostics).Code);
    }

    [Fact]
    public void InvalidSyntaxStopsBeforeRuntimeExecution()
    {
        var result = new VectorEngine().Execute("let value = ; print(\"never\");");

        Assert.False(result.Success);
        Assert.NotEmpty(result.Diagnostics);
        Assert.Empty(result.Output);
    }

    [Fact]
    public void DivisionByZeroIsStructuredEndToEnd()
    {
        var result = new VectorEngine().Execute("10 / 0;");

        Assert.False(result.Success);
        Assert.Equal(DiagnosticCode.DivisionByZero, Assert.Single(result.Diagnostics).Code);
    }

    [Fact]
    public void InvalidListIndexIsStructuredEndToEnd()
    {
        var result = new VectorEngine().Execute("let values = [1, 2]; values[1.5];");

        Assert.False(result.Success);
        Assert.Equal(DiagnosticCode.InvalidListIndex, Assert.Single(result.Diagnostics).Code);
    }

    [Fact]
    public void VectorLengthMismatchIsStructuredEndToEnd()
    {
        var result = new VectorEngine().Execute("[1, 2] + [3];");

        Assert.False(result.Success);
        Assert.Equal(DiagnosticCode.VectorLengthMismatch, Assert.Single(result.Diagnostics).Code);
    }

    [Fact]
    public void UndefinedVariableIsStructuredEndToEnd()
    {
        var result = new VectorEngine().Execute("missing;");

        Assert.False(result.Success);
        Assert.Equal(DiagnosticCode.UndefinedVariable, Assert.Single(result.Diagnostics).Code);
    }

    [Fact]
    public void OutputBeforeRuntimeFailureIsStillCaptured()
    {
        var result = new VectorEngine().Execute("print(\"before\"); 10 + \"bad\";");

        Assert.False(result.Success);
        Assert.Equal(new[] { "before" }, result.Output);
    }

    [Fact]
    public void MissingModuleIsStructuredEndToEnd()
    {
        using var program = new TemporaryProgram();

        var result = new VectorEngine().Execute("import missing;", program.Root);

        Assert.False(result.Success);
        Assert.Equal(DiagnosticCode.ModuleNotFound, Assert.Single(result.Diagnostics).Code);
    }

    [Fact]
    public void ImportedSyntaxErrorCarriesImportedFileAndSource()
    {
        using var program = new TemporaryProgram();
        const string moduleSource = "let broken = ;";
        var modulePath = program.WriteModule("lib.bad", moduleSource);

        var result = new VectorEngine().Execute("import lib.bad;", program.Root);

        Assert.False(result.Success);
        var diagnostic = Assert.Single(result.Diagnostics);
        Assert.Equal(Path.GetFullPath(modulePath), diagnostic.SourceName);
        Assert.Equal(moduleSource, diagnostic.SourceText);

        var formatted = CliDiagnosticFormatter.Format(diagnostic, "entry.vec", "import lib.bad;");
        Assert.Contains(Path.GetFullPath(modulePath), formatted);
        Assert.Contains(moduleSource, formatted);
        Assert.DoesNotContain("    import lib.bad;", formatted);
    }

    [Fact]
    public void ImportedTopLevelRuntimeErrorCarriesImportedFileAndSource()
    {
        using var program = new TemporaryProgram();
        const string moduleSource = "let value = 10 + \"bad\";";
        var modulePath = program.WriteModule("lib.bad", moduleSource);

        var result = new VectorEngine().Execute("import lib.bad;", program.Root);

        Assert.False(result.Success);
        var diagnostic = Assert.Single(result.Diagnostics);
        Assert.Equal(DiagnosticCode.RuntimeTypeError, diagnostic.Code);
        Assert.Equal(Path.GetFullPath(modulePath), diagnostic.SourceName);
        Assert.Equal(moduleSource, diagnostic.SourceText);
    }

    [Fact]
    public void RuntimeErrorInsideImportedFunctionCarriesDefiningModuleSource()
    {
        using var program = new TemporaryProgram();
        const string moduleSource = "function fail() { return 10 + \"bad\"; }";
        var modulePath = program.WriteModule("lib.bad", moduleSource);

        var result = new VectorEngine().Execute("import lib.bad; lib.bad.fail();", program.Root);

        Assert.False(result.Success);
        var diagnostic = Assert.Single(result.Diagnostics);
        Assert.Equal(DiagnosticCode.RuntimeTypeError, diagnostic.Code);
        Assert.Equal(Path.GetFullPath(modulePath), diagnostic.SourceName);
        Assert.Equal(moduleSource, diagnostic.SourceText);
    }

    [Fact]
    public void RuntimeErrorInDependencyKeepsDependencySourceThroughCallerModule()
    {
        using var program = new TemporaryProgram();
        const string dependencySource = "function fail() { return 10 + \"bad\"; }";
        var dependencyPath = program.WriteModule("dep.bad", dependencySource);
        program.WriteModule(
            "feature",
            "import dep.bad; function run() { return dep.bad.fail(); }");

        var result = new VectorEngine().Execute("import feature; feature.run();", program.Root);

        Assert.False(result.Success);
        var diagnostic = Assert.Single(result.Diagnostics);
        Assert.Equal(Path.GetFullPath(dependencyPath), diagnostic.SourceName);
        Assert.Equal(dependencySource, diagnostic.SourceText);
    }

    [Fact]
    public void ReplFormatsImportedRuntimeErrorAgainstModuleSourceAndContinues()
    {
        using var program = new TemporaryProgram();
        const string moduleSource = "let value = 10 + \"bad\";";
        var modulePath = program.WriteModule("lib.bad", moduleSource);
        var output = new StringWriter();
        var error = new StringWriter();
        var repl = new Vector.Cli.Repl(
            new StringReader("import lib.bad;\n2 + 3;\n:exit\n"),
            output,
            error,
            program.Root);

        var exitCode = repl.Run();

        Assert.Equal(0, exitCode);
        Assert.Contains(Path.GetFullPath(modulePath), error.ToString());
        Assert.Contains(moduleSource, error.ToString());
        Assert.Contains("5", output.ToString());
    }

    [Fact]
    public void ReplPersistentFunctionErrorUsesDefinitionSubmissionSource()
    {
        var output = new StringWriter();
        var error = new StringWriter();
        var repl = new Vector.Cli.Repl(
            new StringReader(
                "function fail() {\n" +
                "    return 10 + \"bad\";\n" +
                "}\n" +
                "fail();\n" +
                ":exit\n"),
            output,
            error);

        var exitCode = repl.Run();

        Assert.Equal(0, exitCode);
        Assert.Contains("<repl>:", error.ToString());
        Assert.Contains("return 10 + \"bad\";", error.ToString());
        Assert.DoesNotContain("    fail();", error.ToString());
    }

    [Fact]
    public void ImportedModuleMemberStillCannotBeUsedUnqualified()
    {
        using var program = new TemporaryProgram();
        program.WriteModule("settings", "let value = 42;");

        var result = new VectorEngine().Execute("import settings; value;", program.Root);

        Assert.False(result.Success);
        Assert.Equal(DiagnosticCode.UndefinedVariable, Assert.Single(result.Diagnostics).Code);
    }

    private sealed class TemporaryProgram : IDisposable
    {
        public TemporaryProgram()
        {
            Root = Path.Combine(Path.GetTempPath(), $"VectorErrorIntegration-{Guid.NewGuid():N}");
            Directory.CreateDirectory(Root);
        }

        public string Root { get; }

        public string WriteModule(string qualifiedName, string source)
        {
            var relativePath = qualifiedName.Replace('.', Path.DirectorySeparatorChar) + ".vec";
            var path = Path.Combine(Root, relativePath);
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
