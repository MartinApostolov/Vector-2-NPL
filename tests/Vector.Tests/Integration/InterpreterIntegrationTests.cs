using Vector.Core;
using Vector.Core.Runtime.Values;
using Xunit;

namespace Vector.Tests.Integration;

public sealed class InterpreterIntegrationTests
{
    [Fact]
    public void VariablesCanChangeRuntimeTypeAndUseExplicitConversion()
    {
        const string source = """
            let value = "20";
            value = number(value);
            value = value + 5;
            value;
            """;

        var result = new VectorEngine().Execute(source);

        Assert.True(result.Success);
        Assert.Equal(new NumberValue(25), result.Result);
    }

    [Fact]
    public void NestedScopesShadowWithoutReplacingOuterBinding()
    {
        const string source = """
            let result = 0;
            let value = 10;
            if true {
                let value = 20;
                if value == 20 {
                    result = value;
                }
            }
            [result, value];
            """;

        var result = new VectorEngine().Execute(source);

        Assert.True(result.Success);
        Assert.Equal(
            new ListValue(new VectorValue[] { new NumberValue(20), new NumberValue(10) }),
            result.Result);
    }

    [Fact]
    public void ListsVectorOperationsForLoopAndConcatWorkTogether()
    {
        const string source = """
            let values = [1, 2, 3];
            let scaled = values * 2;
            let transformed = [];
            for item in scaled {
                transformed = concat(transformed, [item + 1]);
            }
            transformed;
            """;

        var result = new VectorEngine().Execute(source);

        Assert.True(result.Success);
        Assert.Equal(
            new ListValue(new VectorValue[]
            {
                new NumberValue(3),
                new NumberValue(5),
                new NumberValue(7)
            }),
            result.Result);
    }

    [Fact]
    public void WhileBreakAndContinueComposeCorrectly()
    {
        const string source = """
            let i = 0;
            let total = 0;
            while i < 6 {
                i = i + 1;
                if i == 2 { continue; }
                if i == 5 { break; }
                total = total + i;
            }
            total;
            """;

        var result = new VectorEngine().Execute(source);

        Assert.True(result.Success);
        Assert.Equal(new NumberValue(8), result.Result);
    }

    [Fact]
    public void ClosuresCaptureLexicalStateAndCanAssignGlobalBinding()
    {
        const string source = """
            let calls = 0;
            function makeAdder(base) {
                function add(value) {
                    calls = calls + 1;
                    return base + value;
                }
                return add;
            }
            let addTen = makeAdder(10);
            [addTen(5), addTen(2), calls];
            """;

        var result = new VectorEngine().Execute(source);

        Assert.True(result.Success);
        Assert.Equal(
            new ListValue(new VectorValue[]
            {
                new NumberValue(15),
                new NumberValue(12),
                new NumberValue(2)
            }),
            result.Result);
    }

    [Fact]
    public void RecursiveFunctionAndBooleanConditionExecuteEndToEnd()
    {
        const string source = """
            function factorial(value) {
                if value <= 1 { return 1; }
                return value * factorial(value - 1);
            }
            factorial(5);
            """;

        var result = new VectorEngine().Execute(source);

        Assert.True(result.Success);
        Assert.Equal(new NumberValue(120), result.Result);
    }

    [Fact]
    public void BuiltinsComposeAcrossCollectionsTextAndNumbers()
    {
        const string source = """
            let values = concat(range(1, 4), [4]);
            let label = text(length(values));
            [values, label, number(label)];
            """;

        var result = new VectorEngine().Execute(source);

        Assert.True(result.Success);
        Assert.Equal(
            new ListValue(new VectorValue[]
            {
                new ListValue(new VectorValue[]
                {
                    new NumberValue(1),
                    new NumberValue(2),
                    new NumberValue(3),
                    new NumberValue(4)
                }),
                new TextValue("4"),
                new NumberValue(4)
            }),
            result.Result);
    }

    [Fact]
    public void NumericListEligibilityTracksCurrentContentsAcrossStatements()
    {
        const string source = """
            let values = [1, 2];
            values[1] = "two";
            values[1] = 2;
            values * 3;
            """;

        var result = new VectorEngine().Execute(source);

        Assert.True(result.Success);
        Assert.Equal(
            new ListValue(new VectorValue[] { new NumberValue(3), new NumberValue(6) }),
            result.Result);
    }

    [Fact]
    public void QualifiedModuleFunctionCanParticipateInMainProgramFlow()
    {
        using var program = new TemporaryProgram();
        program.WriteModule("lib.math", "function double(value) { return value * 2; }");

        const string source = """
            import lib.math;
            let total = 0;
            for value in range(1, 4) {
                total = total + lib.math.double(value);
            }
            total;
            """;

        var result = new VectorEngine().Execute(source, program.Root);

        Assert.True(result.Success);
        Assert.Equal(new NumberValue(12), result.Result);
    }

    [Fact]
    public void SharedModuleDependencyInitializesOnlyOnceEndToEnd()
    {
        using var program = new TemporaryProgram();
        program.WriteModule("shared", "print(\"shared-loaded\"); let value = 3;");
        program.WriteModule("left", "import shared; let value = shared.value + 1;");
        program.WriteModule("right", "import shared; let value = shared.value + 2;");

        var result = new VectorEngine().Execute(
            "import left; import right; [left.value, right.value];",
            program.Root);

        Assert.True(result.Success);
        Assert.Equal(new[] { "shared-loaded" }, result.Output);
        Assert.Equal(
            new ListValue(new VectorValue[] { new NumberValue(4), new NumberValue(5) }),
            result.Result);
    }

    [Fact]
    public void ImportedModuleInternalsRemainOutOfUnqualifiedMainScope()
    {
        using var program = new TemporaryProgram();
        program.WriteModule("settings", "let value = 42;");

        var qualified = new VectorEngine().Execute("import settings; settings.value;", program.Root);

        Assert.True(qualified.Success);
        Assert.Equal(new NumberValue(42), qualified.Result);
    }

    [Fact]
    public void OutputAndFinalValueAreBothPreservedOnSuccessfulExecution()
    {
        const string source = """
            print("begin");
            let value = 6 * 7;
            print(value);
            value;
            """;

        var result = new VectorEngine().Execute(source);

        Assert.True(result.Success);
        Assert.Equal(new[] { "begin", "42" }, result.Output);
        Assert.Equal(new NumberValue(42), result.Result);
    }

    private sealed class TemporaryProgram : IDisposable
    {
        public TemporaryProgram()
        {
            Root = Path.Combine(Path.GetTempPath(), $"VectorIntegration-{Guid.NewGuid():N}");
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
