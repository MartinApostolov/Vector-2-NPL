using Vector.Cli;
using Vector.Core;
using Vector.Core.Diagnostics;
using Vector.Core.Execution;
using Vector.Core.Modules;
using Vector.Core.Runtime.Values;
using Vector.Core.StandardLibrary;
using Vector.Core.StandardLibrary.Math;
using Xunit;

namespace Vector.Tests.StandardLibrary;

public sealed class MathModuleTests
{
    [Fact]
    public void DefaultStandardLibraryRegistersLibMath()
    {
        var registry = StandardLibraryRegistry.CreateDefault();

        Assert.True(registry.TryGet(MathModule.Id, out var definition));
        Assert.NotNull(definition);
        Assert.Equal("lib.math", definition!.QualifiedNamespace);
    }

    [Fact]
    public void DefaultEngineExposesPiAndEAsQualifiedValues()
    {
        var result = Execute("[lib.math.pi, lib.math.e]");

        Assert.True(result.Success);
        var list = Assert.IsType<ListValue>(result.Result);
        Assert.Equal(System.Math.PI, Assert.IsType<NumberValue>(list.Elements[0]).Value);
        Assert.Equal(System.Math.E, Assert.IsType<NumberValue>(list.Elements[1]).Value);
    }

    [Fact]
    public void AbsReturnsAbsoluteValue()
    {
        AssertNumber("lib.math.abs(-10)", 10);
    }

    [Fact]
    public void SqrtReturnsSquareRoot()
    {
        AssertNumber("lib.math.sqrt(25)", 5);
    }

    [Fact]
    public void MinReturnsSmallerValue()
    {
        AssertNumber("lib.math.min(3, 7)", 3);
    }

    [Fact]
    public void MaxReturnsLargerValue()
    {
        AssertNumber("lib.math.max(3, 7)", 7);
    }

    [Fact]
    public void PowReturnsExponentiationResult()
    {
        AssertNumber("lib.math.pow(2, 8)", 256);
    }

    [Fact]
    public void WrongArgumentTypeProducesRuntimeTypeError()
    {
        var result = Execute("lib.math.sqrt(\"25\")");

        Assert.False(result.Success);
        Assert.Equal(DiagnosticCode.RuntimeTypeError, Assert.Single(result.Diagnostics).Code);
    }

    [Fact]
    public void WrongArgumentCountUsesNormalCallArityDiagnostic()
    {
        var result = Execute("lib.math.max(1)");

        Assert.False(result.Success);
        Assert.Equal(DiagnosticCode.ArgumentCountMismatch, Assert.Single(result.Diagnostics).Code);
    }

    [Fact]
    public void InvalidSqrtResultIsRejectedAtNativeBoundary()
    {
        var result = Execute("lib.math.sqrt(-1)");

        Assert.False(result.Success);
        var diagnostic = Assert.Single(result.Diagnostics);
        Assert.Equal(DiagnosticCode.NativeRuntimeFailure, diagnostic.Code);
        Assert.Contains("non-finite", diagnostic.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void OverflowingPowResultIsRejectedAtNativeBoundary()
    {
        var result = Execute("lib.math.pow(10, 1000)");

        Assert.False(result.Success);
        Assert.Equal(DiagnosticCode.NativeRuntimeFailure, Assert.Single(result.Diagnostics).Code);
    }

    [Fact]
    public void ImportDoesNotLeakUnqualifiedPi()
    {
        var result = new VectorEngine().Execute("import lib.math; pi;");

        Assert.False(result.Success);
        Assert.Equal(DiagnosticCode.UndefinedVariable, Assert.Single(result.Diagnostics).Code);
    }

    [Fact]
    public void ImportDoesNotLeakUnqualifiedSqrt()
    {
        var result = new VectorEngine().Execute("import lib.math; sqrt(9);");

        Assert.False(result.Success);
        Assert.Equal(DiagnosticCode.UndefinedVariable, Assert.Single(result.Diagnostics).Code);
    }

    [Fact]
    public void StandardMathCoexistsWithDifferentLocalSourceModule()
    {
        using var program = new TemporaryProgram();
        program.WriteModule("local.values", "let value = 5;");

        var result = new VectorEngine().Execute(
            "import lib.math; import local.values; lib.math.sqrt(16) + local.values.value;",
            program.Root);

        Assert.True(result.Success);
        Assert.Equal(new NumberValue(9), result.Result);
    }

    [Fact]
    public void LocalLibMathSourceProducesExplicitConflict()
    {
        using var program = new TemporaryProgram();
        program.WriteModule("lib.math", "let replacement = 1;");

        var result = new VectorEngine().Execute("import lib.math;", program.Root);

        Assert.False(result.Success);
        Assert.Equal(DiagnosticCode.ModuleConflict, Assert.Single(result.Diagnostics).Code);
    }

    [Fact]
    public void DefaultReplCanUseLibMath()
    {
        var output = new StringWriter();
        var error = new StringWriter();
        var repl = new Vector.Cli.Repl(
            new StringReader("import lib.math;\nlib.math.sqrt(81);\n:exit\n"),
            output,
            error);

        var exitCode = repl.Run();

        Assert.Equal(0, exitCode);
        Assert.Contains("9", output.ToString());
        Assert.Empty(error.ToString());
    }

    private static ExecutionResult Execute(string expression) =>
        new VectorEngine().Execute($"import lib.math; {expression};");

    private static void AssertNumber(string expression, double expected)
    {
        var result = Execute(expression);

        Assert.True(result.Success);
        Assert.Equal(expected, Assert.IsType<NumberValue>(result.Result).Value);
    }

    private sealed class TemporaryProgram : IDisposable
    {
        public TemporaryProgram()
        {
            Root = Path.Combine(Path.GetTempPath(), $"VectorMathModule-{Guid.NewGuid():N}");
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
