using Vector.Core;
using Vector.Core.Diagnostics;
using Vector.Core.Execution;
using Vector.Core.Modules;
using Vector.Core.Modules.Native;
using Vector.Core.Runtime.Host;
using Vector.Core.Runtime.Values;
using Xunit;

namespace Vector.Tests.Execution;

public sealed class VectorVmEngineTests
{
    [Fact]
    public void ExecuteReturnsFinalProgramValue()
    {
        var result = new VectorVmEngine().Execute("let value = 20; value + 5;");

        Assert.True(result.Success);
        Assert.Empty(result.Diagnostics);
        Assert.Equal(new NumberValue(25), result.Result);
    }

    [Fact]
    public void EmptySourceReturnsNothing()
    {
        var result = new VectorVmEngine().Execute(string.Empty);

        Assert.True(result.Success);
        Assert.Same(NothingValue.Instance, result.Result);
        Assert.Empty(result.Diagnostics);
        Assert.Empty(result.Output);
    }

    [Fact]
    public void ExecuteCapturesAndForwardsOutput()
    {
        var forwarded = new List<string>();
        var host = new VectorHost(forwarded.Add);

        var result = new VectorVmEngine().Execute(
            "print(\"one\"); print(\"two\");",
            host: host);

        Assert.True(result.Success);
        Assert.Equal(new[] { "one", "two" }, result.Output);
        Assert.Equal(result.Output, forwarded);
    }

    [Fact]
    public void ExecutePreservesInputCapabilityWhileCapturingOutput()
    {
        var forwarded = new List<string>();
        var host = new VectorInputHost(forwarded.Add, () => "Ada");

        var result = new VectorVmEngine().Execute(
            "import lib.io; let name = lib.io.readLine(); print(name); name;",
            host: host);

        Assert.True(result.Success);
        Assert.Equal(new TextValue("Ada"), result.Result);
        Assert.Equal(new[] { "Ada" }, result.Output);
        Assert.Equal(result.Output, forwarded);
    }

    [Fact]
    public void LexerFailureIsReturnedWithoutExecutingProgram()
    {
        var result = new VectorVmEngine().Execute("print(\"should not run\"); @;");

        Assert.False(result.Success);
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code == DiagnosticCode.InvalidCharacter);
        Assert.Null(result.Result);
        Assert.Empty(result.Output);
    }

    [Fact]
    public void ParserFailureIsReturnedWithoutExecutingProgram()
    {
        var result = new VectorVmEngine().Execute("print(\"should not run\"); let value = ;");

        Assert.False(result.Success);
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Severity == DiagnosticSeverity.Error);
        Assert.Null(result.Result);
        Assert.Empty(result.Output);
    }

    [Theory]
    [InlineData("1 + \"two\";", DiagnosticCode.RuntimeTypeError)]
    [InlineData("missing;", DiagnosticCode.UndefinedVariable)]
    [InlineData("[1][5];", DiagnosticCode.ListIndexOutOfRange)]
    [InlineData("print();", DiagnosticCode.ArgumentCountMismatch)]
    public void RuntimeFailuresAreReturnedAsStructuredDiagnostics(string source, DiagnosticCode expectedCode)
    {
        var result = new VectorVmEngine().Execute(source);

        Assert.False(result.Success);
        var diagnostic = Assert.Single(result.Diagnostics);
        Assert.Equal(expectedCode, diagnostic.Code);
        Assert.Equal(DiagnosticSeverity.Error, diagnostic.Severity);
        Assert.True(diagnostic.Span.Length > 0);
        Assert.Null(result.Result);
    }

    [Fact]
    public void RuntimeFailurePreservesOutputProducedBeforeFailure()
    {
        var result = new VectorVmEngine().Execute("print(\"before\"); 1 / 0;");

        Assert.False(result.Success);
        Assert.Equal(new[] { "before" }, result.Output);
        Assert.Equal(DiagnosticCode.DivisionByZero, Assert.Single(result.Diagnostics).Code);
    }

    [Fact]
    public void EngineUsesVmAwareSourceModuleExecution()
    {
        using var program = new TemporaryProgram();
        program.WriteModule(
            "feature",
            "import lib.math; let base = 40; " +
            "function next(value) { return base + value + lib.math.sqrt(1); }");

        var result = new VectorVmEngine().Execute(
            "import feature; [feature.next(1), feature.next(2)];",
            program.Root);

        Assert.True(result.Success);
        Assert.Equal(
            new ListValue(new VectorValue[]
            {
                new NumberValue(42),
                new NumberValue(43)
            }),
            result.Result);
    }

    [Fact]
    public void RuntimeFailureInsideVmSourceModuleKeepsModuleSourceAttribution()
    {
        using var program = new TemporaryProgram();
        const string moduleSource = "function fail() { return 1 / 0; }";
        program.WriteModule("broken", moduleSource);

        var result = new VectorVmEngine().Execute("import broken; broken.fail();", program.Root);

        Assert.False(result.Success);
        var diagnostic = Assert.Single(result.Diagnostics);
        Assert.Equal(DiagnosticCode.DivisionByZero, diagnostic.Code);
        Assert.Equal(program.ModulePath("broken"), diagnostic.SourceName);
        Assert.Equal(moduleSource, diagnostic.SourceText);
    }

    [Fact]
    public void MissingAndCircularModulesUseSameStructuredEngineDiagnostics()
    {
        using var missingProgram = new TemporaryProgram();
        var missing = new VectorVmEngine().Execute("import missing.module;", missingProgram.Root);
        Assert.False(missing.Success);
        Assert.Equal(DiagnosticCode.ModuleNotFound, Assert.Single(missing.Diagnostics).Code);

        using var circularProgram = new TemporaryProgram();
        circularProgram.WriteModule("a", "import b;");
        circularProgram.WriteModule("b", "import a;");
        var circular = new VectorVmEngine().Execute("import a;", circularProgram.Root);
        Assert.False(circular.Success);
        var circularDiagnostic = Assert.Single(circular.Diagnostics);
        Assert.Equal(DiagnosticCode.CircularImport, circularDiagnostic.Code);
        Assert.Contains("a -> b -> a", circularDiagnostic.Message);
    }

    [Fact]
    public void InjectedNativeRegistryIsUsedByVmEngine()
    {
        var registry = new NativeModuleRegistry();
        registry.Register(new NativeModuleDefinition(
            new ModuleId(new[] { "custom", "values" }),
            module => module.Export("answer", new NumberValue(42))));
        var engine = new VectorVmEngine(registry);

        var result = engine.Execute("import custom.values; custom.values.answer;");

        Assert.Same(registry, engine.NativeModules);
        Assert.True(result.Success);
        Assert.Equal(new NumberValue(42), result.Result);
    }

    [Fact]
    public void CompileExposesDeterministicDisassemblyWithoutExecutingSource()
    {
        var engine = new VectorVmEngine();

        var compilation = engine.Compile("print(\"must not run\"); let value = 1 + 2; value;", "debug.vec");

        Assert.True(compilation.Success);
        Assert.Empty(compilation.Diagnostics);
        var disassembly = Assert.IsType<string>(compilation.Disassembly);
        Assert.Contains("source: debug.vec", disassembly);
        Assert.Contains("Add", disassembly);
        Assert.Contains("Call", disassembly);
    }

    [Fact]
    public void CompileReturnsStructuredSyntaxDiagnosticsAndNoDisassemblyOnFailure()
    {
        var compilation = new VectorVmEngine().Compile("let value = ;", "broken.vec");

        Assert.False(compilation.Success);
        Assert.NotEmpty(compilation.Diagnostics);
        Assert.Null(compilation.Disassembly);
        Assert.All(compilation.Diagnostics, diagnostic =>
        {
            Assert.Equal("broken.vec", diagnostic.SourceName);
            Assert.Equal("let value = ;", diagnostic.SourceText);
        });
    }

    [Fact]
    public void CompileRejectsWhitespaceSourceName()
    {
        Assert.Throws<ArgumentException>(() => new VectorVmEngine().Compile("1;", "   "));
    }

    [Fact]
    public void VmAndInterpreterEnginesMatchOnRepresentativeLanguageProgram()
    {
        const string source = """
            function double(value) { return value * 2; }
            let values = [];
            for number in range(1, 4) {
                values = concat(values, [double(number)]);
            }
            if length(values) == 3 and values[2] == 6 {
                values[1] = values[1] + 10;
            }
            values;
            """;

        var interpreter = new VectorEngine().Execute(source);
        var vm = new VectorVmEngine().Execute(source);

        AssertEquivalent(interpreter, vm);
        Assert.Equal(
            new ListValue(new VectorValue[]
            {
                new NumberValue(2),
                new NumberValue(14),
                new NumberValue(6)
            }),
            vm.Result);
    }

    [Fact]
    public void VmAndInterpreterEnginesMatchOnRuntimeFailure()
    {
        const string source = "let values = [1]; print(\"before\"); values[5];";

        var interpreter = new VectorEngine().Execute(source);
        var vm = new VectorVmEngine().Execute(source);

        AssertEquivalent(interpreter, vm);
    }

    private static void AssertEquivalent(ExecutionResult expected, ExecutionResult actual)
    {
        Assert.Equal(expected.Success, actual.Success);
        Assert.Equal(expected.Result, actual.Result);
        Assert.Equal(expected.Output, actual.Output);
        Assert.Equal(expected.Diagnostics.Count, actual.Diagnostics.Count);

        for (var index = 0; index < expected.Diagnostics.Count; index++)
        {
            Assert.Equal(expected.Diagnostics[index].Code, actual.Diagnostics[index].Code);
            Assert.Equal(expected.Diagnostics[index].Severity, actual.Diagnostics[index].Severity);
            Assert.Equal(expected.Diagnostics[index].Message, actual.Diagnostics[index].Message);
            Assert.Equal(expected.Diagnostics[index].Span, actual.Diagnostics[index].Span);
        }
    }

    private sealed class TemporaryProgram : IDisposable
    {
        public TemporaryProgram()
        {
            Root = Path.Combine(Path.GetTempPath(), $"VectorVmEngineTests-{Guid.NewGuid():N}");
            Directory.CreateDirectory(Root);
        }

        public string Root { get; }

        public string ModulePath(string qualifiedName)
        {
            var segments = qualifiedName.Split('.');
            var fileName = segments[^1] + ".vec";
            var directory = segments.Length == 1
                ? Root
                : Path.Combine(new[] { Root }.Concat(segments[..^1]).ToArray());
            return Path.GetFullPath(Path.Combine(directory, fileName));
        }

        public void WriteModule(string qualifiedName, string source)
        {
            var path = ModulePath(qualifiedName);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, source);
        }

        public void Dispose()
        {
            try
            {
                Directory.Delete(Root, recursive: true);
            }
            catch (DirectoryNotFoundException)
            {
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
        }
    }
}
