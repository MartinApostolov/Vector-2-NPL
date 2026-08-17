using Vector.Core;
using Vector.Core.Diagnostics;
using Vector.Core.Runtime.Host;
using Vector.Core.Runtime.Values;
using Xunit;

namespace Vector.Tests.Integration;

public sealed class StandardLibraryIntegrationTests
{
    [Fact]
    public void TypeBuiltinWorksInOrdinaryProgramFlow()
    {
        var result = new VectorEngine().Execute(
            "let values = [1, 2, 3]; [type(values), type(values[0]), type(nothing)];");

        Assert.True(result.Success);
        Assert.Equal(
            new ListValue(new VectorValue[]
            {
                new TextValue("list"),
                new TextValue("number"),
                new TextValue("nothing")
            }),
            result.Result);
    }

    [Fact]
    public void CollectionsAcceptLocalVariablesAndFunctionResults()
    {
        const string source = """
            import lib.collections;
            function makeValues() { return [2, 4, 6]; }
            let values = makeValues();
            [lib.collections.sum(values), lib.collections.min(values), lib.collections.max(makeValues())];
            """;

        var result = new VectorEngine().Execute(source);

        Assert.True(result.Success);
        Assert.Equal(
            new ListValue(new VectorValue[]
            {
                new NumberValue(12),
                new NumberValue(2),
                new NumberValue(6)
            }),
            result.Result);
    }

    [Fact]
    public void InputModuleReadsThroughConfiguredHostAlongsideOtherStandardModules()
    {
        var host = new VectorInputHost(null, () => "3");
        var result = new VectorEngine().Execute(
            "import lib.io; import lib.math; let value = number(lib.io.readLine()); lib.math.pow(value, 2);",
            host: host);

        Assert.True(result.Success);
        Assert.Equal(new NumberValue(9), result.Result);
    }

    [Fact]
    public void LocalSourceModuleCanCallLibVector()
    {
        using var program = new TemporaryProgram();
        program.WriteModule(
            "local.vector_tools",
            "import lib.vector; function similarity(a, b) { return lib.vector.dot(a, b); }");

        var result = new VectorEngine().Execute(
            "import local.vector_tools; local.vector_tools.similarity([1, 2, 3], [4, 5, 6]);",
            program.Root);

        Assert.True(result.Success);
        Assert.Equal(new NumberValue(32), result.Result);
    }

    [Fact]
    public void LocalSourceModuleCanCallLibMatrix()
    {
        using var program = new TemporaryProgram();
        program.WriteModule(
            "local.matrix_tools",
            "import lib.matrix; function combine(a, b) { return lib.matrix.multiply(a, b); }");

        var result = new VectorEngine().Execute(
            "import local.matrix_tools; local.matrix_tools.combine([[1, 2]], [[3], [4]]);",
            program.Root);

        Assert.True(result.Success);
        Assert.Equal(Matrix(new[] { new[] { 11d } }), result.Result);
    }

    [Fact]
    public void SeveralStandardModulesAndLocalSourceModuleCoexist()
    {
        using var program = new TemporaryProgram();
        program.WriteModule(
            "local.values",
            "function vector() { return [3, 4]; } function matrix() { return [[1, 2], [3, 4]]; }");

        const string source = """
            import lib.collections;
            import lib.math;
            import lib.vector;
            import lib.matrix;
            import local.values;
            let vector = local.values.vector();
            let matrix = local.values.matrix();
            [
                lib.collections.sum(vector),
                lib.math.sqrt(81),
                lib.vector.magnitude(vector),
                lib.matrix.shape(matrix)
            ];
            """;

        var result = new VectorEngine().Execute(source, program.Root);

        Assert.True(result.Success);
        Assert.Equal(
            new ListValue(new VectorValue[]
            {
                new NumberValue(7),
                new NumberValue(9),
                new NumberValue(5),
                new ListValue(new VectorValue[] { new NumberValue(2), new NumberValue(2) })
            }),
            result.Result);
    }

    [Fact]
    public void VectorLibraryResultsRemainOrdinaryListsForVectorArithmetic()
    {
        var result = new VectorEngine().Execute(
            "import lib.vector; let unit = lib.vector.normalize([3, 4]); [unit + [1, 1], type(unit)];");

        Assert.True(result.Success);
        Assert.Equal(
            new ListValue(new VectorValue[]
            {
                new ListValue(new VectorValue[] { new NumberValue(1.6), new NumberValue(1.8) }),
                new TextValue("list")
            }),
            result.Result);
    }

    [Fact]
    public void MatrixResultsCanBeIndexedAndPassedThroughUserFunctions()
    {
        const string source = """
            import lib.matrix;
            function identity(value) { return value; }
            let product = identity(lib.matrix.multiply([[1, 2], [3, 4]], [[5, 6], [7, 8]]));
            [product[0][1], product[1][0], type(product)];
            """;

        var result = new VectorEngine().Execute(source);

        Assert.True(result.Success);
        Assert.Equal(
            new ListValue(new VectorValue[]
            {
                new NumberValue(22),
                new NumberValue(43),
                new TextValue("list")
            }),
            result.Result);
    }

    [Fact]
    public void SourceNativeNameConflictRemainsExplicitWithExpandedLibrary()
    {
        using var program = new TemporaryProgram();
        program.WriteModule("lib.vector", "let replacement = 1;");

        var result = new VectorEngine().Execute("import lib.vector;", program.Root);

        Assert.False(result.Success);
        Assert.Equal(DiagnosticCode.ModuleConflict, Assert.Single(result.Diagnostics).Code);
    }

    [Theory]
    [InlineData("import lib.collections;", "lib.collections.sum([1, \"x\"])", DiagnosticCode.RuntimeTypeError)]
    [InlineData("import lib.collections;", "lib.collections.min([])", DiagnosticCode.NativeRuntimeFailure)]
    [InlineData("import lib.collections;", "lib.collections.max([])", DiagnosticCode.NativeRuntimeFailure)]
    [InlineData("import lib.vector;", "lib.vector.dot([1, 2], [3])", DiagnosticCode.VectorLengthMismatch)]
    [InlineData("import lib.vector;", "lib.vector.normalize([0, 0])", DiagnosticCode.NativeRuntimeFailure)]
    [InlineData("import lib.matrix;", "lib.matrix.shape([1, 2])", DiagnosticCode.RuntimeTypeError)]
    [InlineData("import lib.matrix;", "lib.matrix.transpose([[1, 2], [3]])", DiagnosticCode.RuntimeTypeError)]
    [InlineData("import lib.matrix;", "lib.matrix.add([[1, 2]], [[1], [2]])", DiagnosticCode.NativeRuntimeFailure)]
    [InlineData("import lib.matrix;", "lib.matrix.multiply([[1, 2, 3]], [[1], [2]])", DiagnosticCode.NativeRuntimeFailure)]
    public void ExpandedLibraryFailuresStayStructuredAndRetainCallSite(
        string import,
        string call,
        DiagnosticCode expectedCode)
    {
        var source = $"{import}\nlet value = {call};";

        var result = new VectorEngine().Execute(source);

        Assert.False(result.Success);
        var diagnostic = Assert.Single(result.Diagnostics);
        Assert.Equal(expectedCode, diagnostic.Code);
        Assert.Equal(source.IndexOf(call, StringComparison.Ordinal), diagnostic.Span.Start.Offset);
        Assert.Equal(call.Length, diagnostic.Span.Length);
        Assert.DoesNotContain("System.", diagnostic.Message);
        Assert.DoesNotContain(" at Vector.", diagnostic.Message);
    }

    [Fact]
    public void UnsupportedInputHostStaysStructuredAndRetainsCallSite()
    {
        const string call = "lib.io.readLine()";
        var source = $"import lib.io;\n{call};";

        var result = new VectorEngine().Execute(source, host: new VectorHost());

        Assert.False(result.Success);
        var diagnostic = Assert.Single(result.Diagnostics);
        Assert.Equal(DiagnosticCode.NativeRuntimeFailure, diagnostic.Code);
        Assert.Equal(source.IndexOf(call, StringComparison.Ordinal), diagnostic.Span.Start.Offset);
        Assert.Equal(call.Length, diagnostic.Span.Length);
        Assert.DoesNotContain("System.", diagnostic.Message);
    }

    private static ListValue Matrix(double[][] rows) =>
        new(rows.Select(row =>
            (VectorValue)new ListValue(row.Select(value => (VectorValue)new NumberValue(value)))));

    private sealed class TemporaryProgram : IDisposable
    {
        public TemporaryProgram()
        {
            Root = Path.Combine(Path.GetTempPath(), $"VectorStandardLibraryIntegration-{Guid.NewGuid():N}");
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
