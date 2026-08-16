using Vector.Cli;
using Vector.Core;
using Vector.Core.Diagnostics;
using Vector.Core.Modules;
using Vector.Core.Modules.Native;
using Vector.Core.Runtime.Native;
using Vector.Core.Runtime.Values;
using Vector.Core.StandardLibrary;
using Xunit;

namespace Vector.Tests.Integration;

public sealed class NativeLibraryIntegrationTests
{
    [Fact]
    public void MainProgramCanImportAndUseLibMath()
    {
        var result = new VectorEngine().Execute(
            "import lib.math; [lib.math.sqrt(25), lib.math.abs(-10), lib.math.max(3, 7)];");

        Assert.True(result.Success);
        Assert.Equal(
            new ListValue(new VectorValue[]
            {
                new NumberValue(5),
                new NumberValue(10),
                new NumberValue(7)
            }),
            result.Result);
    }

    [Fact]
    public void LocalSourceModuleCanImportAndCallLibMath()
    {
        using var program = new TemporaryProgram();
        program.WriteModule(
            "local.geometry",
            "import lib.math; function root(value) { return lib.math.sqrt(value); }");

        var result = new VectorEngine().Execute(
            "import local.geometry; local.geometry.root(81);",
            program.Root);

        Assert.True(result.Success);
        Assert.Equal(new NumberValue(9), result.Result);
    }

    [Fact]
    public void MainProgramCanUseLocalSourceModuleAndLibMathTogether()
    {
        using var program = new TemporaryProgram();
        program.WriteModule("local.values", "let exponent = 3;");

        var result = new VectorEngine().Execute(
            "import lib.math; import local.values; lib.math.pow(2, local.values.exponent);",
            program.Root);

        Assert.True(result.Success);
        Assert.Equal(new NumberValue(8), result.Result);
    }

    [Fact]
    public void VectorFunctionCanCallNativeMathFunction()
    {
        const string source = """
            import lib.math;
            function hypotenuse(a, b) {
                return lib.math.sqrt((a * a) + (b * b));
            }
            hypotenuse(3, 4);
            """;

        var result = new VectorEngine().Execute(source);

        Assert.True(result.Success);
        Assert.Equal(new NumberValue(5), result.Result);
    }

    [Fact]
    public void NativeMathCallsWorkInsideLoopsAndLists()
    {
        const string source = """
            import lib.math;
            let squares = [];
            for value in range(1, 4) {
                squares = concat(squares, [lib.math.pow(value, 2)]);
            }
            squares;
            """;

        var result = new VectorEngine().Execute(source);

        Assert.True(result.Success);
        Assert.Equal(
            new ListValue(new VectorValue[]
            {
                new NumberValue(1),
                new NumberValue(4),
                new NumberValue(9)
            }),
            result.Result);
    }

    [Fact]
    public void NativeMathResultsParticipateInNormalVectorExpressions()
    {
        var result = new VectorEngine().Execute(
            "import lib.math; [lib.math.sqrt(16) + 1, lib.math.max(2, 5) * 2, lib.math.pi > 3];");

        Assert.True(result.Success);
        Assert.Equal(
            new ListValue(new VectorValue[]
            {
                new NumberValue(5),
                new NumberValue(10),
                new BooleanValue(true)
            }),
            result.Result);
    }

    [Fact]
    public void DefaultReplCanReuseLibMathAcrossSubmissions()
    {
        var output = new StringWriter();
        var error = new StringWriter();
        var repl = new Vector.Cli.Repl(
            new StringReader(
                "import lib.math;\n" +
                "lib.math.sqrt(81);\n" +
                "lib.math.max(10, 25);\n" +
                "lib.math.pi;\n" +
                ":exit\n"),
            output,
            error);

        var exitCode = repl.Run();

        Assert.Equal(0, exitCode);
        Assert.Contains("9", output.ToString());
        Assert.Contains("25", output.ToString());
        Assert.Contains(System.Math.PI.ToString("G", System.Globalization.CultureInfo.InvariantCulture), output.ToString());
        Assert.Empty(error.ToString());
    }

    [Fact]
    public void SourceNativeNameConflictRemainsExplicitEndToEnd()
    {
        using var program = new TemporaryProgram();
        program.WriteModule("lib.math", "let replacement = 1;");

        var result = new VectorEngine().Execute("import lib.math;", program.Root);

        Assert.False(result.Success);
        var diagnostic = Assert.Single(result.Diagnostics);
        Assert.Equal(DiagnosticCode.ModuleConflict, diagnostic.Code);
        Assert.Contains("both a local Vector source file and a registered native module", diagnostic.Message);
    }

    [Fact]
    public void WrongNativeArgumentTypeRetainsVectorCallSiteSpan()
    {
        const string call = "lib.math.sqrt(\"25\")";
        var source = $"import lib.math;\nlet value = {call};";

        var result = new VectorEngine().Execute(source);

        Assert.False(result.Success);
        var diagnostic = Assert.Single(result.Diagnostics);
        Assert.Equal(DiagnosticCode.RuntimeTypeError, diagnostic.Code);
        Assert.Equal(source.IndexOf(call, StringComparison.Ordinal), diagnostic.Span.Start.Offset);
        Assert.Equal(call.Length, diagnostic.Span.Length);
    }

    [Fact]
    public void WrongNativeArgumentCountRetainsVectorCallSiteSpan()
    {
        const string call = "lib.math.max(1)";
        var source = $"import lib.math;\n{call};";

        var result = new VectorEngine().Execute(source);

        Assert.False(result.Success);
        var diagnostic = Assert.Single(result.Diagnostics);
        Assert.Equal(DiagnosticCode.ArgumentCountMismatch, diagnostic.Code);
        Assert.Equal(source.IndexOf(call, StringComparison.Ordinal), diagnostic.Span.Start.Offset);
        Assert.Equal(call.Length, diagnostic.Span.Length);
    }

    [Fact]
    public void NativeMathFailureRetainsVectorCallSiteSpan()
    {
        const string call = "lib.math.sqrt(-1)";
        var source = $"import lib.math;\n{call};";

        var result = new VectorEngine().Execute(source);

        Assert.False(result.Success);
        var diagnostic = Assert.Single(result.Diagnostics);
        Assert.Equal(DiagnosticCode.NativeRuntimeFailure, diagnostic.Code);
        Assert.Equal(source.IndexOf(call, StringComparison.Ordinal), diagnostic.Span.Start.Offset);
        Assert.Equal(call.Length, diagnostic.Span.Length);
        Assert.Contains("non-finite", diagnostic.Message.ToLowerInvariant());
    }

    [Fact]
    public void UnexpectedHostExceptionBecomesSafeStructuredVectorDiagnostic()
    {
        var registry = StandardLibraryRegistry.CreateDefault();
        registry.Register(new NativeModuleDefinition(
            Id("test.failure"),
            context => context.Export(
                "explode",
                new NativeFunction(
                    "explode",
                    0,
                    (_, _) => throw new InvalidOperationException("SECRET HOST DETAIL")))));
        const string call = "test.failure.explode()";
        var source = $"import test.failure;\n{call};";

        var result = new VectorEngine(registry).Execute(source);

        Assert.False(result.Success);
        var diagnostic = Assert.Single(result.Diagnostics);
        Assert.Equal(DiagnosticCode.NativeRuntimeFailure, diagnostic.Code);
        Assert.Equal(source.IndexOf(call, StringComparison.Ordinal), diagnostic.Span.Start.Offset);
        Assert.Equal(call.Length, diagnostic.Span.Length);
        Assert.Contains("Native function 'explode' failed", diagnostic.Message);
        Assert.DoesNotContain("SECRET HOST DETAIL", diagnostic.Message);
        Assert.DoesNotContain("InvalidOperationException", diagnostic.Message);
    }

    [Fact]
    public void NativeMathExampleExecutesThroughNormalEnginePath()
    {
        var repositoryRoot = FindRepositoryRoot();
        var examplePath = Path.Combine(repositoryRoot, "examples", "11_native_math.vec");
        var source = File.ReadAllText(examplePath);

        var result = new VectorEngine().Execute(source, Path.GetDirectoryName(examplePath));

        Assert.True(result.Success);
        Assert.Equal(
            new[]
            {
                "5",
                "10",
                "7",
                "3",
                "256",
                System.Math.PI.ToString("G", System.Globalization.CultureInfo.InvariantCulture),
                System.Math.E.ToString("G", System.Globalization.CultureInfo.InvariantCulture)
            },
            result.Output);
    }

    private static ModuleId Id(string qualifiedName) =>
        new(qualifiedName.Split('.', StringSplitOptions.RemoveEmptyEntries));

    private static string FindRepositoryRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory);
             directory is not null;
             directory = directory.Parent)
        {
            if (File.Exists(Path.Combine(directory.FullName, "Vector.sln")))
            {
                return directory.FullName;
            }
        }

        throw new DirectoryNotFoundException("Could not locate the Vector repository root containing Vector.sln.");
    }

    private sealed class TemporaryProgram : IDisposable
    {
        public TemporaryProgram()
        {
            Root = Path.Combine(Path.GetTempPath(), $"VectorNativeLibraryIntegration-{Guid.NewGuid():N}");
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
