using Vector.Core;
using Vector.Core.Execution;
using Vector.Core.Runtime.Host;
using Vector.Core.Runtime.Values;
using Xunit;

namespace Vector.Tests.Integration;

public sealed class VmCompatibilityTests
{
    public static IEnumerable<object[]> SuccessfulPrograms =>
        new[]
        {
            Case("(1 + 2) * 3 == 9 and not false;"),
            Case("let outer = 10; { let outer = 20; outer = outer + 1; } outer;"),
            Case("let values = [1, 2, 3]; values[1] = 10; values * 2;"),
            Case("if true and not false { 42; } else { 0; }"),
            Case("let total = 0; for value in [1, 2, 3, 4] { if value == 3 { continue; } total = total + value; } total;"),
            Case("function fact(n) { if n <= 1 { return 1; } return n * fact(n - 1); } fact(6);"),
            Case("function makeCounter() { let value = 0; function next() { value = value + 1; return value; } return next; } let c = makeCounter(); [c(), c(), c()];"),
            Case("[length(range(1, 5)), concat([1], [2, 3]), text(12), number(\"7\"), type([1])];"),
            Case("import lib.math; import lib.collections; import lib.vector; import lib.matrix; [lib.math.sqrt(81), lib.collections.sum([1, 2, 3]), lib.vector.dot([1, 2], [3, 4]), lib.matrix.shape([[1, 2], [3, 4]])];")
        };

    [Theory]
    [MemberData(nameof(SuccessfulPrograms))]
    public void InterpreterAndVmMatchAcrossCurrentLanguageFeatures(string source)
    {
        CompatibilityAssert.Success(source);
    }

    [Fact]
    public void DeterministicInputAndOutputMatch()
    {
        const string source = "import lib.io; let name = lib.io.readLine(); print(concat([name], [\"!\"])); name;";
        var interpreterInput = new Queue<string?>(new[] { "Ada" });
        var vmInput = new Queue<string?>(new[] { "Ada" });
        var interpreterHost = new VectorInputHost(null, () => interpreterInput.Dequeue());
        var vmHost = new VectorInputHost(null, () => vmInput.Dequeue());

        CompatibilityAssert.Success(source, interpreterHost: interpreterHost, vmHost: vmHost);
        Assert.Empty(interpreterInput);
        Assert.Empty(vmInput);
    }

    [Fact]
    public void DisassemblyIsDeterministicForCoreControlAndModuleInstructions()
    {
        const string source = """
            import lib.math;
            function inc(value) { return value + 1; }
            let total = 0;
            while total < 2 { total = inc(total); }
            if total == 2 { lib.math.sqrt(81); } else { 0; }
            """;
        var engine = new VectorVmEngine();

        var first = engine.Compile(source, "compat.vec");
        var second = engine.Compile(source, "compat.vec");

        Assert.True(first.Success);
        Assert.Equal(first.Disassembly, second.Disassembly);
        var disassembly = Assert.IsType<string>(first.Disassembly);
        Assert.Contains("Import", disassembly);
        Assert.Contains("MakeClosure", disassembly);
        Assert.Contains("Call", disassembly);
        Assert.Contains("Add", disassembly);
        Assert.Contains("Jump", disassembly);
        Assert.Contains("JumpIfFalse", disassembly);
    }

    private static object[] Case(string source) => new object[] { source };
}

internal static class CompatibilityAssert
{
    public static void Success(
        string source,
        string? programRoot = null,
        IVectorHost? interpreterHost = null,
        IVectorHost? vmHost = null)
    {
        var interpreter = new VectorEngine().Execute(source, programRoot, interpreterHost);
        var vm = new VectorVmEngine().Execute(source, programRoot, vmHost);

        Assert.True(interpreter.Success, DiagnosticText(interpreter));
        Assert.True(vm.Success, DiagnosticText(vm));
        Equivalent(interpreter, vm);
    }

    public static void Failure(string source, string? programRoot = null)
    {
        var interpreter = new VectorEngine().Execute(source, programRoot);
        var vm = new VectorVmEngine().Execute(source, programRoot);

        Assert.False(interpreter.Success);
        Assert.False(vm.Success);
        Equivalent(interpreter, vm);
    }

    public static void Equivalent(ExecutionResult expected, ExecutionResult actual)
    {
        Assert.Equal(expected.Success, actual.Success);
        Assert.Equal(expected.Result, actual.Result);
        Assert.Equal(expected.Output, actual.Output);
        Assert.Equal(expected.Diagnostics.Count, actual.Diagnostics.Count);

        for (var index = 0; index < expected.Diagnostics.Count; index++)
        {
            var left = expected.Diagnostics[index];
            var right = actual.Diagnostics[index];
            Assert.Equal(left.Code, right.Code);
            Assert.Equal(left.Severity, right.Severity);
            Assert.Equal(left.Message, right.Message);
            Assert.Equal(left.Span, right.Span);
            Assert.Equal(left.SourceName, right.SourceName);
            Assert.Equal(left.SourceText, right.SourceText);
        }
    }

    private static string DiagnosticText(ExecutionResult result) =>
        string.Join(System.Environment.NewLine, result.Diagnostics.Select(diagnostic => $"{diagnostic.Code}: {diagnostic.Message}"));
}
